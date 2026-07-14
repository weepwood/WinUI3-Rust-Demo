using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using WinUI3RustDemo.Models;
using WinUI3RustDemo.Services;
using static Microsoft.UI.Reactor.Factories;

namespace WinUI3RustDemo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AppBootstrap.InitialSnapshot = SystemBridge.ReadSnapshot();
        ReactorApp.Run<SystemCenterApp>("WinUI 3 Rust System Center", width: 1180, height: 760);
    }
}

internal static class AppBootstrap
{
    public static SnapshotResult InitialSnapshot { get; set; } = new(
        false,
        SystemSnapshot.Fallback("Application is starting."),
        "正在初始化系统信息。");
}

internal enum AppRoute
{
    Overview,
    System,
    Tools,
    Settings,
    About,
}

internal sealed class SystemCenterApp : Component
{
    public override Element Render()
    {
        var (route, setRoute) = UseState(AppRoute.Overview, threadSafe: true);
        var initialSettings = UseMemo(
            () => SettingsStore.Load() with { StartWithWindows = StartupRegistration.IsEnabled() },
            Array.Empty<object>());
        var (settings, setSettings) = UseState(initialSettings, threadSafe: true);
        var (snapshot, setSnapshot) = UseState(AppBootstrap.InitialSnapshot.Snapshot, threadSafe: true);
        var (status, setStatus) = UseState(AppBootstrap.InitialSnapshot.Message, threadSafe: true);
        var (pendingAction, setPendingAction) = UseState<string?>(null, threadSafe: true);

        UseEffect(() =>
        {
            SettingsStore.Save(settings);
            return () => { };
        }, settings);

        UseEffect(() =>
        {
            if (!settings.AutoRefresh)
            {
                return () => { };
            }

            var cancellation = new CancellationTokenSource();
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.RefreshIntervalSeconds));
            _ = Task.Run(async () =>
            {
                try
                {
                    while (await timer.WaitForNextTickAsync(cancellation.Token))
                    {
                        var result = SystemBridge.ReadSnapshot();
                        setSnapshot(result.Snapshot);
                        setStatus(result.Success
                            ? $"自动刷新完成：{DateTime.Now:HH:mm:ss}"
                            : result.Message);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            return () =>
            {
                cancellation.Cancel();
                timer.Dispose();
                cancellation.Dispose();
            };
        }, settings.AutoRefresh, settings.RefreshIntervalSeconds);

        void RefreshSnapshot()
        {
            setStatus("正在通过 Rust 桥接刷新系统信息……");
            _ = Task.Run(() =>
            {
                var result = SystemBridge.ReadSnapshot();
                setSnapshot(result.Snapshot);
                setStatus(result.Success
                    ? $"系统信息已刷新：{DateTime.Now:HH:mm:ss}"
                    : result.Message);
            });
        }

        void RunOperation(Func<OperationResult> operation, string progressText)
        {
            setStatus(progressText);
            _ = Task.Run(() =>
            {
                var result = operation();
                setStatus(result.Message);
            });
        }

        void UpdateStartup(bool enabled)
        {
            var result = StartupRegistration.SetEnabled(enabled);
            setStatus(result.Message);
            if (result.Success)
            {
                setSettings(settings with { StartWithWindows = enabled });
            }
        }

        void RunProtectedAction(string actionId, Func<OperationResult> operation, string progressText)
        {
            if (settings.ConfirmSystemActions && pendingAction != actionId)
            {
                setPendingAction(actionId);
                setStatus("此操作会调用 Windows 系统命令。请再次点击同一按钮确认执行。");
                return;
            }

            setPendingAction(null);
            RunOperation(operation, progressText);
        }

        // Avoid TitleBar.IconSource: some unpackaged/self-contained WinUI systems
        // reject generated FontIconSource values during the first Reactor mount.
        var titleBar = TitleBar("WinUI 3 Rust System Center")
            .Flex(shrink: 0);

        Element page = route switch
        {
            AppRoute.Overview => OverviewPage(snapshot, settings, RefreshSnapshot),
            AppRoute.System => SystemPage(snapshot, settings),
            AppRoute.Tools => ToolsPage(
                status,
                pendingAction,
                () => RunOperation(SystemBridge.RunDoctor, "正在运行 Rust 桥接诊断……"),
                () => RunOperation(SystemBridge.ExportReport, "正在导出系统报告……"),
                () => RunProtectedAction("flush-dns", SystemBridge.FlushDns, "正在刷新 DNS 缓存……"),
                message => setStatus(message)),
            AppRoute.Settings => SettingsPage(settings, setSettings, UpdateStartup, message => setStatus(message)),
            AppRoute.About => AboutPage(snapshot),
            _ => TextBlock("页面不存在。"),
        };

        // Microsoft.UI.Reactor preview.11 assigns NavigationView.SelectedItem during
        // mount. On some unpackaged/self-contained systems that native setter throws
        // FileNotFoundException while resolving WinUI resources. A button-based shell
        // keeps navigation deterministic without touching NavigationView.SelectedItem.
        var sidebar = Border(
                VStack(8,
                    TextBlock("系统中心").FontSize(18).SemiBold().Margin(bottom: 8),
                    SidebarButton("概览", AppRoute.Overview, route, setRoute),
                    SidebarButton("系统信息", AppRoute.System, route, setRoute),
                    SidebarButton("工具", AppRoute.Tools, route, setRoute),
                    SidebarButton("设置", AppRoute.Settings, route, setRoute),
                    SidebarButton("关于", AppRoute.About, route, setRoute)))
            .Background(Theme.LayerFill)
            .WithBorder(Theme.DividerStroke)
            .Padding(14)
            .Width(220)
            .Flex(shrink: 0);

        var content = FlexRow(
                sidebar,
                Border(page).Flex(grow: 1, basis: 0))
            .Flex(grow: 1, basis: 0);

        var statusBar = Border(
                HStack(8,
                    TextBlock("状态").SemiBold(),
                    Caption(status).Flex(grow: 1, basis: 0),
                    Caption($"Bridge {snapshot.BridgeVersion}")))
            .Background(Theme.LayerFill)
            .WithBorder(Theme.DividerStroke)
            .Padding(10)
            .Flex(shrink: 0);

        Element root = FlexColumn(titleBar, content, statusBar)
            .Backdrop(BackdropKind.Mica);

        return settings.ThemeMode switch
        {
            1 => root.RequestedTheme(ElementTheme.Light),
            2 => root.RequestedTheme(ElementTheme.Dark),
            _ => root,
        };
    }

    private static Element SidebarButton(
        string label,
        AppRoute target,
        AppRoute current,
        Action<AppRoute> navigate)
        => Button(current == target ? $"●  {label}" : $"    {label}", () => navigate(target))
            .HorizontalContentAlignment(HorizontalAlignment.Left)
            .Width(190);

    private static Element OverviewPage(SystemSnapshot snapshot, AppSettings settings, Action refresh)
    {
        var hostname = settings.HideHostname ? "••••••••" : snapshot.Hostname;
        var spacing = settings.CompactLayout ? 10 : 16;
        var padding = settings.CompactLayout ? 16 : 24;

        return ScrollView(
            VStack(spacing,
                HStack(12,
                    VStack(4,
                        Heading("系统概览"),
                        Caption($"{hostname} · {snapshot.OsName} {snapshot.OsVersion}"))
                        .Flex(grow: 1, basis: 0),
                    Button("刷新", refresh)),
                HStack(spacing,
                    MetricCard("CPU", $"{snapshot.Cpu.UsagePercent:F1}%", snapshot.Cpu.Brand, settings.CompactLayout),
                    MetricCard("内存", $"{snapshot.Memory.UsagePercent:F1}%", $"{FormatBytes(snapshot.Memory.UsedBytes)} / {FormatBytes(snapshot.Memory.TotalBytes)}", settings.CompactLayout),
                    MetricCard("运行时间", FormatDuration(snapshot.UptimeSeconds), $"进程 {snapshot.ProcessCount}", settings.CompactLayout)),
                SectionCard("设备与系统",
                    VStack(10,
                        InfoRow("设备名称", hostname),
                        InfoRow("操作系统", $"{snapshot.OsName} {snapshot.OsVersion}"),
                        InfoRow("内核版本", snapshot.KernelVersion),
                        InfoRow("体系结构", snapshot.Architecture),
                        InfoRow("CPU 核心", $"{snapshot.Cpu.PhysicalCores} 物理 / {snapshot.Cpu.LogicalCores} 逻辑"))),
                SubHeading("磁盘概览"),
                ForEach(snapshot.Disks, disk => DiskCard(disk, settings.CompactLayout))
            ).Padding(padding));
    }

    private static Element SystemPage(SystemSnapshot snapshot, AppSettings settings)
    {
        var hostname = settings.HideHostname ? "••••••••" : snapshot.Hostname;
        return ScrollView(
            VStack(16,
                Heading("系统信息"),
                Caption("数据由随应用发布的 Rust 桥接程序读取。"),
                SectionCard("操作系统",
                    VStack(10,
                        InfoRow("设备", hostname),
                        InfoRow("系统", $"{snapshot.OsName} {snapshot.OsVersion}"),
                        InfoRow("内核", snapshot.KernelVersion),
                        InfoRow("架构", snapshot.Architecture),
                        InfoRow("启动时间", FormatUnixTime(snapshot.BootTimeSeconds)),
                        InfoRow("运行时间", FormatDuration(snapshot.UptimeSeconds)))),
                SectionCard("处理器",
                    VStack(10,
                        InfoRow("型号", snapshot.Cpu.Brand),
                        InfoRow("物理核心", snapshot.Cpu.PhysicalCores.ToString()),
                        InfoRow("逻辑核心", snapshot.Cpu.LogicalCores.ToString()),
                        InfoRow("最高频率", snapshot.Cpu.FrequencyMhz == 0 ? "未知" : $"{snapshot.Cpu.FrequencyMhz} MHz"),
                        InfoRow("当前占用", $"{snapshot.Cpu.UsagePercent:F1}%"))),
                SectionCard("内存",
                    VStack(10,
                        InfoRow("总内存", FormatBytes(snapshot.Memory.TotalBytes)),
                        InfoRow("已使用", FormatBytes(snapshot.Memory.UsedBytes)),
                        InfoRow("可用", FormatBytes(snapshot.Memory.AvailableBytes)),
                        InfoRow("使用率", $"{snapshot.Memory.UsagePercent:F1}%"))),
                SubHeading("磁盘"),
                ForEach(snapshot.Disks, disk => DiskCard(disk, settings.CompactLayout))
            ).Padding(settings.CompactLayout ? 16 : 24));
    }

    private static Element ToolsPage(
        string status,
        string? pendingAction,
        Action runDoctor,
        Action exportReport,
        Action flushDns,
        Action<string> setStatus)
    {
        void Open(Func<OperationResult> action)
        {
            var result = action();
            setStatus(result.Message);
        }

        return ScrollView(
            VStack(16,
                Heading("工具与快捷入口"),
                Caption("快捷入口使用 Windows 的 ms-settings URI；系统命令仅限桥接程序中的固定白名单。"),
                SectionCard("Windows 设置",
                    VStack(12,
                        HStack(10,
                            Button("显示设置", () => Open(() => SystemBridge.OpenSettings("ms-settings:display"))).Width(180),
                            Button("存储设置", () => Open(() => SystemBridge.OpenSettings("ms-settings:storagesense"))).Width(180),
                            Button("网络设置", () => Open(() => SystemBridge.OpenSettings("ms-settings:network-status"))).Width(180)),
                        HStack(10,
                            Button("Windows 更新", () => Open(() => SystemBridge.OpenSettings("ms-settings:windowsupdate"))).Width(180),
                            Button("隐私设置", () => Open(() => SystemBridge.OpenSettings("ms-settings:privacy"))).Width(180),
                            Button("已安装应用", () => Open(() => SystemBridge.OpenSettings("ms-settings:appsfeatures"))).Width(180)))),
                SectionCard("诊断与报告",
                    VStack(12,
                        HStack(10,
                            Button("运行桥接诊断", runDoctor).Width(180),
                            Button("导出 JSON 报告", exportReport).Width(180),
                            Button("打开任务管理器", () => Open(SystemBridge.OpenTaskManager)).Width(180)),
                        HStack(10,
                            Button("打开临时目录", () => Open(() => SystemBridge.OpenFolder(Path.GetTempPath()))).Width(180),
                            Button(pendingAction == "flush-dns" ? "再次点击刷新 DNS" : "刷新 DNS 缓存", flushDns).Width(180)))),
                SectionCard("最近状态", TextBlock(status))
            ).Padding(24));
    }

    private static Element SettingsPage(
        AppSettings settings,
        Action<AppSettings> setSettings,
        Action<bool> setStartup,
        Action<string> setStatus)
    {
        void ResetSettings()
        {
            if (settings.StartWithWindows)
            {
                var result = StartupRegistration.SetEnabled(false);
                setStatus(result.Message);
            }
            setSettings(AppSettings.Default);
            setStatus("应用设置已恢复默认值。");
        }

        return ScrollView(
            VStack(16,
                Heading("设置"),
                Caption("设置写入当前用户的 LocalAppData，不需要管理员权限。"),
                SectionCard("外观",
                    VStack(14,
                        SettingsRow("主题", "选择应用的明暗模式。",
                            ComboBox(["跟随系统", "浅色", "深色"], settings.ThemeMode,
                                value => setSettings(settings with { ThemeMode = value })).Width(180)),
                        SettingsRow("紧凑布局", "减少卡片间距与页面留白。",
                            ToggleSwitch(settings.CompactLayout,
                                value => setSettings(settings with { CompactLayout = value }))),
                        SettingsRow("隐藏设备名称", "界面中使用遮罩替代主机名。",
                            ToggleSwitch(settings.HideHostname,
                                value => setSettings(settings with { HideHostname = value }))))),
                SectionCard("数据刷新",
                    VStack(14,
                        SettingsRow("自动刷新", "定期重新读取 CPU、内存和磁盘信息。",
                            ToggleSwitch(settings.AutoRefresh,
                                value => setSettings(settings with { AutoRefresh = value }))),
                        SettingsRow("刷新间隔", $"当前 {settings.RefreshIntervalSeconds} 秒。",
                            Slider(settings.RefreshIntervalSeconds, 5, 60,
                                value => setSettings(settings with { RefreshIntervalSeconds = Math.Max(5, (int)Math.Round(value)) }))
                                .Width(220)))),
                SectionCard("系统集成",
                    VStack(14,
                        SettingsRow("开机启动", "仅为当前 Windows 用户注册启动项。",
                            ToggleSwitch(settings.StartWithWindows, setStartup)),
                        SettingsRow("确认系统命令", "DNS 刷新等操作需要连续点击两次。",
                            ToggleSwitch(settings.ConfirmSystemActions,
                                value => setSettings(settings with { ConfirmSystemActions = value }))))),
                HStack(10,
                    Button("恢复默认设置", ResetSettings),
                    Button("打开应用数据目录", () =>
                    {
                        var path = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "WinUI3RustSystemCenter");
                        Directory.CreateDirectory(path);
                        setStatus(SystemBridge.OpenFolder(path).Message);
                    }))
            ).Padding(settings.CompactLayout ? 16 : 24));
    }

    private static Element AboutPage(SystemSnapshot snapshot)
        => ScrollView(
            VStack(16,
                Heading("关于"),
                SectionCard("WinUI 3 Rust System Center",
                    VStack(10,
                        TextBlock("使用 Microsoft.UI.Reactor 构建声明式 WinUI 3 界面，并使用 Rust 收集系统信息。"),
                        InfoRow("应用版本", "0.1.0"),
                        InfoRow("Rust Bridge", snapshot.BridgeVersion),
                        InfoRow("UI 框架", "Microsoft.UI.Reactor 0.1.0-preview.11"),
                        InfoRow("Windows App SDK", "2.1.3"))),
                SectionCard("设计边界",
                    VStack(8,
                        TextBlock("不提供任意命令执行。"),
                        TextBlock("不默认请求管理员权限。"),
                        TextBlock("所有系统修改均应明确、可见并可回滚。")))
            ).Padding(24));

    private static Element MetricCard(string title, string value, string detail, bool compact)
        => Border(
                VStack(compact ? 5 : 8,
                    Caption(title),
                    TextBlock(value).FontSize(compact ? 24 : 30).SemiBold(),
                    Caption(detail)))
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke)
            .CornerRadius(12)
            .Padding(compact ? 12 : 16)
            .Width(compact ? 220 : 250);

    private static Element SectionCard(string title, Element content)
        => Border(
                VStack(12,
                    SubHeading(title),
                    content))
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke)
            .CornerRadius(12)
            .Padding(18);

    private static Element DiskCard(DiskSnapshot disk, bool compact)
    {
        var used = Math.Max(0, disk.TotalBytes - disk.AvailableBytes);
        return Border(
                VStack(compact ? 6 : 9,
                    HStack(10,
                        TextBlock(string.IsNullOrWhiteSpace(disk.MountPoint) ? disk.Name : disk.MountPoint).SemiBold(),
                        Caption(disk.FileSystem).Flex(grow: 1, basis: 0),
                        TextBlock($"{disk.UsagePercent:F1}%")),
                    Caption($"已使用 {FormatBytes(used)} · 可用 {FormatBytes(disk.AvailableBytes)} · 总计 {FormatBytes(disk.TotalBytes)}")))
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke)
            .CornerRadius(10)
            .Padding(compact ? 12 : 16);
    }

    private static Element InfoRow(string label, string value)
        => HStack(14,
            Caption(label).Width(150),
            TextBlock(value).Flex(grow: 1, basis: 0));

    private static Element SettingsRow(string title, string description, Element control)
        => HStack(20,
            VStack(4,
                TextBlock(title).SemiBold(),
                Caption(description))
                .Flex(grow: 1, basis: 0),
            control);

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:F1} {units[unit]}";
    }

    private static string FormatDuration(long seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return duration.TotalDays >= 1
            ? $"{(int)duration.TotalDays} 天 {duration.Hours} 小时"
            : $"{duration.Hours} 小时 {duration.Minutes} 分钟";
    }

    private static string FormatUnixTime(long seconds)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return "未知";
        }
    }
}
