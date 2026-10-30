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

        HotkeyText.Text = controller.IsHotkeyRegistered
            ? "Strg+Alt+Z"
            : "Strg+Alt+Z ist belegt – bitte den Knopf nutzen";

        // The render thread publishes its status once per second; polling keeps threads decoupled.
        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(500);
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
        var status = _controller.Status;
        ZoomStateText.Text = status.IsRunning
            ? string.Create(CultureInfo.CurrentCulture, $"an ({ZoomController.FixedZoom:0.#}×)")
            : "aus";
        FpsText.Text = status.IsRunning
            ? string.Create(CultureInfo.CurrentCulture, $"{status.FramesPerSecond} fps")
            : "–";
        CaptureText.Text = status.IsRunning ? (status.IsCaptureExcluded ? "ja" : "NEIN") : "–";

        ErrorBar.IsOpen = status.LastError is not null;
        ErrorBar.Message = status.LastError ?? string.Empty;
    }
}
