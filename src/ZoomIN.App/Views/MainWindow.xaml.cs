using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ZoomIN.App.Zoom;

namespace ZoomIN.App.Views;

public sealed partial class MainWindow : Window
{
    private readonly ZoomController _controller;
    private readonly DispatcherQueueTimer _refreshTimer;

    // Public: Microsoft.Extensions.DependencyInjection only uses public constructors.
    public MainWindow(ZoomController controller)
    {
        _controller = controller;
        InitializeComponent();

        HotkeyText.Text = controller.UnavailableHotkeys.Count == 0
            ? "Strg+Alt+Z · Strg+Mausrad · Strg+Alt+Bild↑/↓"
            : "Teilweise belegt: " + string.Join(", ", controller.UnavailableHotkeys);

        // Status is polled: the render thread must never call into the UI.
        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(250);
        _refreshTimer.Tick += (_, _) => Refresh();
        _refreshTimer.Start();
        Closed += (_, _) => _refreshTimer.Stop();
        Refresh();
    }

    private void OnToggleClick(object sender, RoutedEventArgs e)
    {
        _controller.Toggle();
        Refresh();
    }

    private void Refresh()
    {
        var model = _controller.Model;
        var status = _controller.Status;
        var levels = _controller.MonitorIds().Select(id =>
            string.Create(CultureInfo.CurrentCulture, $"{id.TrimStart('\\', '.')} {(model.IsOn ? model.TargetFor(id) : model.LastLevelFor(id)):0.##}×"));
        ZoomStateText.Text = (model.IsOn ? "an: " : "aus, zuletzt: ") + string.Join(" · ", levels);
        FpsText.Text = status.IsVisible
            ? string.Create(CultureInfo.CurrentCulture, $"{status.FramesPerSecond} fps ({status.MonitorCount} Monitor(e))")
            : string.Create(CultureInfo.CurrentCulture, $"– ({status.MonitorCount} Monitor(e) bereit)");
        CaptureText.Text = status.IsCaptureExcluded ? "ja" : "–";

        ErrorBar.IsOpen = status.LastError is not null;
        ErrorBar.Message = status.LastError ?? string.Empty;
    }
}
