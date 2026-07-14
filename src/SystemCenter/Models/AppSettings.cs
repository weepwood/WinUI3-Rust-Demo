namespace WinUI3RustDemo.Models;

public sealed record AppSettings
{
    public bool AutoRefresh { get; init; } = true;
    public int RefreshIntervalSeconds { get; init; } = 15;
    public int ThemeMode { get; init; }
    public bool CompactLayout { get; init; }
    public bool HideHostname { get; init; }
    public bool StartWithWindows { get; init; }
    public bool ConfirmSystemActions { get; init; } = true;

    public static AppSettings Default { get; } = new();
}
