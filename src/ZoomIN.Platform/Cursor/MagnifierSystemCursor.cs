using Windows.Win32;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Platform.Cursor;

/// <summary>
/// Hides the system pointer with MagShowSystemCursor (ADR-0011). Only this cursor function of the
/// Magnification API is used; the display path stays our own overlay (ADR-0002).
/// </summary>
public sealed class MagnifierSystemCursor : ISystemCursor
{
    private readonly ILog _log;
    private readonly Lock _gate = new();
    private readonly bool _available;
    private bool _visible = true;

    public MagnifierSystemCursor(ILog log)
    {
        _log = log;
        _available = PInvoke.MagInitialize();
        if (!_available)
        {
            _log.Warn("MagInitialize failed: the system pointer stays visible while zoomed.");
        }
    }

    public void SetVisible(bool visible)
    {
        lock (_gate)
        {
            if (!_available || visible == _visible)
            {
                return;
            }

            if (PInvoke.MagShowSystemCursor(visible))
            {
                _visible = visible;
            }
        }
    }

    public void Dispose()
    {
        SetVisible(true);
        if (_available)
        {
            PInvoke.MagUninitialize();
        }
    }
}
