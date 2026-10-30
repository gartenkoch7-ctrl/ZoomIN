using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Zoom;

/// <summary>
/// Connects the toggle hotkey and the UI button to the zoom output.
/// M1: fixed 2x zoom; M2 replaces this with animated, adjustable zoom.
/// </summary>
public sealed class ZoomController
{
    public const double FixedZoom = 2.0;

    private readonly IZoomOutput _output;
    private readonly IHotkeyService _hotkeys;
    private readonly ILog _log;
    private readonly Lock _gate = new();

    public ZoomController(IZoomOutput output, IHotkeyService hotkeys, ILog log)
    {
        _output = output;
        _hotkeys = hotkeys;
        _log = log;
    }

    public bool IsHotkeyRegistered { get; private set; }

    public ZoomStatus Status => _output.Status;

    public void Initialize()
    {
        IsHotkeyRegistered = _hotkeys.Register(HotkeyGesture.ToggleZoom, Toggle);
        if (IsHotkeyRegistered)
        {
            _log.Info("Toggle hotkey Ctrl+Alt+Z registered");
        }
        else
        {
            _log.Warn("Toggle hotkey Ctrl+Alt+Z is not available; use the button instead.");
        }
    }

    /// <summary>Called from the hotkey thread and the UI thread.</summary>
    public void Toggle()
    {
        lock (_gate)
        {
            _log.Info(_output.IsRunning ? "Toggle: stopping zoom" : "Toggle: starting zoom");
            if (_output.IsRunning)
            {
                _output.Stop();
            }
            else
            {
                _output.Start(FixedZoom);
            }
        }
    }
}
