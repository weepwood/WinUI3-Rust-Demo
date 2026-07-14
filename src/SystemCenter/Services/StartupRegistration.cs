using Microsoft.Win32;

namespace WinUI3RustDemo.Services;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WinUI3RustSystemCenter";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static OperationResult SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return new(false, "无法打开当前用户启动项注册表路径。");
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return new(true, "已关闭开机启动。");
            }

            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
            {
                return new(false, "无法确定当前程序路径。");
            }

            key.SetValue(ValueName, $"\"{executable}\"", RegistryValueKind.String);
            return new(true, "已为当前用户启用开机启动。");
        }
        catch (Exception exception)
        {
            return new(false, $"更新开机启动失败：{exception.Message}");
        }
    }
}

internal sealed record OperationResult(bool Success, string Message);
