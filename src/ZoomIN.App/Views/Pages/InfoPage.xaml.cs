using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Localization;

namespace ZoomIN.App.Views.Pages;

/// <summary>Live status (former M1–M6 diagnostics window), version, logs and licence.</summary>
public sealed partial class InfoPage : Page
{
    private PageContext? _context;
    private DispatcherQueueTimer? _timer;

    public InfoPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _context = (PageContext)e.Parameter;
        // Informational version keeps "-rc1"; the SDK appends "+<commit>", which users do not need.
        var version = typeof(InfoPage).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
        VersionText.Text = version.Split('+')[0];

        // Status is polled: the render threads never call into the UI.
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => _timer?.Stop();

    private void Refresh()
    {
        var controller = _context!.Controller;
        var model = controller.Model;
        var status = controller.Status;
        var mode = model.Mode;
        var levels = controller.MonitorIds().Select(id => string.Create(
            CultureInfo.CurrentCulture, $"{id.TrimStart('\\', '.')} {(model.IsOn ? model.TargetFor(id) : model.LastLevelFor(mode, id)):0.##}×"));
        ZoomText.Text = $"{AppShell.ModeName(mode)} – {(model.IsOn ? Text.On : Text.Off)}: {string.Join(" · ", levels)}";
        FpsText.Text = status.IsVisible
            ? string.Create(CultureInfo.CurrentCulture, $"{status.FramesPerSecond} fps")
            : "–";
        CaptureText.Text = status.IsCaptureExcluded ? "✓" : "–";
        ErrorBar.IsOpen = status.LastError is not null;
        ErrorBar.Message = status.LastError ?? string.Empty;
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN", "logs");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = false })?.Dispose();
    }
}
