use serde::Serialize;
use serde_json::json;
use std::env;
use std::fs;
use std::path::Path;
use std::process::{Command, ExitCode};
use sysinfo::{Disks, System};

const VERSION: &str = env!("CARGO_PKG_VERSION");

#[derive(Serialize)]
struct CpuSnapshot {
    brand: String,
    physical_cores: usize,
    logical_cores: usize,
    frequency_mhz: u64,
    usage_percent: f32,
}

#[derive(Serialize)]
struct MemorySnapshot {
    total_bytes: u64,
    used_bytes: u64,
    available_bytes: u64,
    usage_percent: f64,
}

#[derive(Serialize)]
struct DiskSnapshot {
    name: String,
    file_system: String,
    mount_point: String,
    total_bytes: u64,
    available_bytes: u64,
    usage_percent: f64,
}

#[derive(Serialize)]
struct SystemSnapshot {
    generated_at: u64,
    hostname: String,
    os_name: String,
    os_version: String,
    kernel_version: String,
    architecture: String,
    cpu: CpuSnapshot,
    memory: MemorySnapshot,
    disks: Vec<DiskSnapshot>,
    process_count: usize,
    uptime_seconds: u64,
    boot_time_seconds: u64,
    bridge_version: &'static str,
}

fn main() -> ExitCode {
    let mut args = env::args().skip(1);
    let command = args.next().unwrap_or_else(|| "snapshot".to_string());

    let result = match command.as_str() {
        "snapshot" => print_snapshot(),
        "doctor" => print_doctor(),
        "export" => match args.next() {
            Some(path) => export_snapshot(Path::new(&path)),
            None => Err("export requires a destination path".to_string()),
        },
        "flush-dns" => flush_dns(),
        "version" => {
            println!("{VERSION}");
            Ok(())
        }
        _ => Err(format!(
            "unsupported command '{command}'. Allowed: snapshot, doctor, export, flush-dns, version"
        )),
    };

    match result {
        Ok(()) => ExitCode::SUCCESS,
        Err(message) => {
            eprintln!("{message}");
            ExitCode::from(1)
        }
    }
}

fn collect_snapshot() -> SystemSnapshot {
    let mut system = System::new_all();
    system.refresh_all();

    let cpus = system.cpus();
    let logical_cores = cpus.len();
    let physical_cores = system.physical_core_count().unwrap_or(logical_cores);
    let brand = cpus
        .first()
        .map(|cpu| cpu.brand().trim().to_string())
        .filter(|value| !value.is_empty())
        .unwrap_or_else(|| "Unknown CPU".to_string());
    let frequency_mhz = cpus.iter().map(|cpu| cpu.frequency()).max().unwrap_or(0);
    let usage_percent = if logical_cores == 0 {
        0.0
    } else {
        cpus.iter().map(|cpu| cpu.cpu_usage()).sum::<f32>() / logical_cores as f32
    };

    let total_memory = system.total_memory();
    let used_memory = system.used_memory();
    let available_memory = system.available_memory();
    let memory_usage = percent(used_memory, total_memory);

    let disks = Disks::new_with_refreshed_list()
        .list()
        .iter()
        .map(|disk| {
            let total = disk.total_space();
            let available = disk.available_space();
            DiskSnapshot {
                name: disk.name().to_string_lossy().into_owned(),
                file_system: disk.file_system().to_string_lossy().into_owned(),
                mount_point: disk.mount_point().to_string_lossy().into_owned(),
                total_bytes: total,
                available_bytes: available,
                usage_percent: percent(total.saturating_sub(available), total),
            }
        })
        .collect();

    SystemSnapshot {
        generated_at: System::boot_time().saturating_add(System::uptime()),
        hostname: System::host_name().unwrap_or_else(|| "Unknown device".to_string()),
        os_name: System::name().unwrap_or_else(|| "Windows".to_string()),
        os_version: System::os_version().unwrap_or_else(|| "Unknown".to_string()),
        kernel_version: System::kernel_version().unwrap_or_else(|| "Unknown".to_string()),
        architecture: env::consts::ARCH.to_string(),
        cpu: CpuSnapshot {
            brand,
            physical_cores,
            logical_cores,
            frequency_mhz,
            usage_percent,
        },
        memory: MemorySnapshot {
            total_bytes: total_memory,
            used_bytes: used_memory,
            available_bytes: available_memory,
            usage_percent: memory_usage,
        },
        disks,
        process_count: system.processes().len(),
        uptime_seconds: System::uptime(),
        boot_time_seconds: System::boot_time(),
        bridge_version: VERSION,
    }
}

fn print_snapshot() -> Result<(), String> {
    let output = serde_json::to_string(&collect_snapshot()).map_err(|error| error.to_string())?;
    println!("{output}");
    Ok(())
}

fn print_doctor() -> Result<(), String> {
    let executable = env::current_exe().map_err(|error| error.to_string())?;
    let current_dir = env::current_dir().map_err(|error| error.to_string())?;
    let snapshot = collect_snapshot();
    let output = json!({
        "ok": true,
        "bridge_version": VERSION,
        "target_os": env::consts::OS,
        "target_arch": env::consts::ARCH,
        "executable": executable,
        "current_directory": current_dir,
        "system_supported": sysinfo::IS_SUPPORTED_SYSTEM,
        "disk_count": snapshot.disks.len(),
        "process_count": snapshot.process_count
    });
    println!(
        "{}",
        serde_json::to_string(&output).map_err(|error| error.to_string())?
    );
    Ok(())
}

fn export_snapshot(path: &Path) -> Result<(), String> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|error| error.to_string())?;
    }
    let output =
        serde_json::to_string_pretty(&collect_snapshot()).map_err(|error| error.to_string())?;
    fs::write(path, output).map_err(|error| error.to_string())?;
    println!("{}", path.display());
    Ok(())
}

fn flush_dns() -> Result<(), String> {
    if !cfg!(target_os = "windows") {
        return Err("flush-dns is available only on Windows".to_string());
    }

    let output = Command::new("ipconfig")
        .arg("/flushdns")
        .output()
        .map_err(|error| format!("failed to launch ipconfig: {error}"))?;

    if output.status.success() {
        let message = String::from_utf8_lossy(&output.stdout).trim().to_string();
        println!(
            "{}",
            if message.is_empty() {
                "DNS cache refreshed"
            } else {
                &message
            }
        );
        Ok(())
    } else {
        let message = String::from_utf8_lossy(&output.stderr).trim().to_string();
        Err(if message.is_empty() {
            format!("ipconfig exited with status {}", output.status)
        } else {
            message
        })
    }
}

fn percent(used: u64, total: u64) -> f64 {
    if total == 0 {
        0.0
    } else {
        used as f64 * 100.0 / total as f64
    }
}
