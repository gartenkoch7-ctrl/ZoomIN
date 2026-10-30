using Windows.Win32;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Polls the cursor with GetCursorPos. Polling once per frame on the render thread is the
/// lowest-latency option (docs/kb/eingabe-und-tracking.md). The process is per-monitor DPI v2
/// aware, so the result is in physical pixels.
/// </summary>
public sealed class Win32CursorSource : ICursorSource
{
    private PhysicalPoint _last;

    public PhysicalPoint GetPosition()
    {
        // Fails e.g. while the secure desktop (UAC, Ctrl+Alt+Del) is active: keep the last position.
        if (PInvoke.GetCursorPos(out var point))
        {
            _last = new PhysicalPoint(point.X, point.Y);
        }

        return _last;
    }
}
