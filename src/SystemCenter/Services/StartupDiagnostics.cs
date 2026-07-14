using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinUI3RustDemo.Services;

internal static class StartupDiagnostics
{
    private const string AppFolderName = "WinUI3RustSystemCenter";
    private static readonly object Gate = new();
    private static int _initialized;

    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName,
        "Diagnostics");

    public static string LogFilePath { get; } = Path.Combine(RootDirectory, "startup.log");

    public static string DumpDirectory { get; } = Path.Combine(RootDirectory, "CrashDumps");

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(DumpDirectory);
        }
        catch
        {
            return;
        }

        Write("diagnostics-initialize");
        Write($"os={Environment.OSVersion}");
        Write($"process-architecture={RuntimeInformation.ProcessArchitecture}");
        Write($"framework={RuntimeInformation.FrameworkDescription}");
        Write($"base-directory={AppContext.BaseDirectory}");
        Write($"current-directory={Environment.CurrentDirectory}");
        Write($"xaml-dll-exists={File.Exists(Path.Combine(AppContext.BaseDirectory, "Microsoft.UI.Xaml.dll"))}");
        Write($"reactor-dll-exists={File.Exists(Path.Combine(AppContext.BaseDirectory, "Reactor.dll"))}");
        Write($"resources-pri-exists={File.Exists(Path.Combine(AppContext.BaseDirectory, "resources.pri"))}");
        ConfigureLocalDumps();
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(RootDirectory);
                File.AppendAllText(
                    LogFilePath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [pid:{Environment.ProcessId}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }

    public static void WriteException(string stage, Exception exception)
    {
        Write($"{stage}: {exception}");
    }

    public static void ShowFatal(Exception exception)
    {
        try
        {
            _ = MessageBoxW(
                0,
                $"应用启动失败。\n\n{exception.Message}\n\n诊断日志：\n{LogFilePath}",
                "WinUI 3 Rust System Center",
                0x00000010);
        }
        catch
        {
        }
    }

    private static void ConfigureLocalDumps()
    {
        try
        {
            const string keyPath = @"Software\Microsoft\Windows\Windows Error Reporting\LocalDumps\WinUI3RustSystemCenter.exe";
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
            if (key is null)
            {
                Write("local-dumps-key-unavailable");
                return;
            }

            key.SetValue("DumpFolder", DumpDirectory, RegistryValueKind.ExpandString);
            key.SetValue("DumpCount", 5, RegistryValueKind.DWord);
            key.SetValue("DumpType", 1, RegistryValueKind.DWord);
            Write($"local-dumps-configured={DumpDirectory}");
        }
        catch (Exception exception)
        {
            WriteException("local-dumps-configuration", exception);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
}
