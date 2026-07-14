using System.Text.Json.Serialization;

namespace WinUI3RustDemo.Models;

public sealed record SystemSnapshot
{
    [JsonPropertyName("generated_at")]
    public long GeneratedAt { get; init; }

    [JsonPropertyName("hostname")]
    public string Hostname { get; init; } = "Unknown device";

    [JsonPropertyName("os_name")]
    public string OsName { get; init; } = "Windows";

    [JsonPropertyName("os_version")]
    public string OsVersion { get; init; } = "Unknown";

    [JsonPropertyName("kernel_version")]
    public string KernelVersion { get; init; } = "Unknown";

    [JsonPropertyName("architecture")]
    public string Architecture { get; init; } = "Unknown";

    [JsonPropertyName("cpu")]
    public CpuSnapshot Cpu { get; init; } = new();

    [JsonPropertyName("memory")]
    public MemorySnapshot Memory { get; init; } = new();

    [JsonPropertyName("disks")]
    public IReadOnlyList<DiskSnapshot> Disks { get; init; } = Array.Empty<DiskSnapshot>();

    [JsonPropertyName("process_count")]
    public int ProcessCount { get; init; }

    [JsonPropertyName("uptime_seconds")]
    public long UptimeSeconds { get; init; }

    [JsonPropertyName("boot_time_seconds")]
    public long BootTimeSeconds { get; init; }

    [JsonPropertyName("bridge_version")]
    public string BridgeVersion { get; init; } = "unavailable";

    public static SystemSnapshot Fallback(string reason) => new()
    {
        GeneratedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Hostname = Environment.MachineName,
        OsName = Environment.OSVersion.Platform.ToString(),
        OsVersion = Environment.OSVersion.VersionString,
        KernelVersion = reason,
        Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
        Cpu = new CpuSnapshot
        {
            Brand = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU",
            LogicalCores = Environment.ProcessorCount,
            PhysicalCores = 0,
        },
        Memory = new MemorySnapshot(),
        ProcessCount = 0,
        UptimeSeconds = Environment.TickCount64 / 1000,
        BridgeVersion = "fallback",
    };
}

public sealed record CpuSnapshot
{
    [JsonPropertyName("brand")]
    public string Brand { get; init; } = "Unknown CPU";

    [JsonPropertyName("physical_cores")]
    public int PhysicalCores { get; init; }

    [JsonPropertyName("logical_cores")]
    public int LogicalCores { get; init; }

    [JsonPropertyName("frequency_mhz")]
    public long FrequencyMhz { get; init; }

    [JsonPropertyName("usage_percent")]
    public double UsagePercent { get; init; }
}

public sealed record MemorySnapshot
{
    [JsonPropertyName("total_bytes")]
    public long TotalBytes { get; init; }

    [JsonPropertyName("used_bytes")]
    public long UsedBytes { get; init; }

    [JsonPropertyName("available_bytes")]
    public long AvailableBytes { get; init; }

    [JsonPropertyName("usage_percent")]
    public double UsagePercent { get; init; }
}

public sealed record DiskSnapshot
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "Disk";

    [JsonPropertyName("file_system")]
    public string FileSystem { get; init; } = "Unknown";

    [JsonPropertyName("mount_point")]
    public string MountPoint { get; init; } = string.Empty;

    [JsonPropertyName("total_bytes")]
    public long TotalBytes { get; init; }

    [JsonPropertyName("available_bytes")]
    public long AvailableBytes { get; init; }

    [JsonPropertyName("usage_percent")]
    public double UsagePercent { get; init; }
}
