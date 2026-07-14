using System.Diagnostics;
using System.Text;
using System.Text.Json;
using WinUI3RustDemo.Models;

namespace WinUI3RustDemo.Services;

internal static class SystemBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static string BridgePath => Path.Combine(AppContext.BaseDirectory, "sysbridge.exe");

    public static SnapshotResult ReadSnapshot()
    {
        var result = Invoke(TimeSpan.FromSeconds(8), "snapshot");
        if (!result.Success)
        {
            return new(false, SystemSnapshot.Fallback(result.Message), result.Message);
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<SystemSnapshot>(result.Output, JsonOptions);
            return snapshot is null
                ? new(false, SystemSnapshot.Fallback("Rust bridge returned empty JSON."), "Rust bridge 返回了空数据。")
                : new(true, snapshot, "系统信息已刷新。");
        }
        catch (Exception exception)
        {
            return new(false, SystemSnapshot.Fallback(exception.Message), $"解析系统信息失败：{exception.Message}");
        }
    }

    public static OperationResult RunDoctor()
    {
        var result = Invoke(TimeSpan.FromSeconds(8), "doctor");
        return new(result.Success, result.Success ? $"桥接诊断通过：{result.Output}" : result.Message);
    }

    public static OperationResult ExportReport()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "WinUI3RustSystemCenter");
        var path = Path.Combine(directory, $"system-report-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var result = Invoke(TimeSpan.FromSeconds(15), "export", path);
        return new(result.Success, result.Success ? $"报告已导出：{path}" : result.Message);
    }

    public static OperationResult FlushDns()
    {
        var result = Invoke(TimeSpan.FromSeconds(15), "flush-dns");
        return new(result.Success, result.Success ? "DNS 缓存刷新命令已完成。" : result.Message);
    }

    public static OperationResult OpenSettings(string settingsUri)
        => OpenShellTarget(settingsUri, "Windows 设置");

    public static OperationResult OpenFolder(string path)
        => OpenShellTarget(path, "文件夹");

    public static OperationResult OpenTaskManager()
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
            return new(true, "已打开任务管理器。");
        }
        catch (Exception exception)
        {
            return new(false, $"打开任务管理器失败：{exception.Message}");
        }
    }

    private static OperationResult OpenShellTarget(string target, string label)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return new(true, $"已打开{label}。");
        }
        catch (Exception exception)
        {
            return new(false, $"打开{label}失败：{exception.Message}");
        }
    }

    private static CommandResult Invoke(TimeSpan timeout, params string[] arguments)
    {
        if (!File.Exists(BridgePath))
        {
            return new(false, string.Empty, $"未找到 Rust 桥接程序：{BridgePath}");
        }

        try
        {
            var startInfo = new ProcessStartInfo(BridgePath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new(false, string.Empty, "无法启动 Rust 桥接程序。");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                return new(false, string.Empty, "Rust 桥接程序执行超时。");
            }

            Task.WaitAll(stdoutTask, stderrTask);
            var stdout = stdoutTask.Result.Trim();
            var stderr = stderrTask.Result.Trim();
            if (process.ExitCode != 0)
            {
                return new(false, stdout, string.IsNullOrWhiteSpace(stderr)
                    ? $"Rust 桥接程序退出码：{process.ExitCode}"
                    : stderr);
            }

            return new(true, stdout, string.Empty);
        }
        catch (Exception exception)
        {
            return new(false, string.Empty, $"调用 Rust 桥接程序失败：{exception.Message}");
        }
    }

    private sealed record CommandResult(bool Success, string Output, string Message);
}

internal sealed record SnapshotResult(bool Success, SystemSnapshot Snapshot, string Message);
