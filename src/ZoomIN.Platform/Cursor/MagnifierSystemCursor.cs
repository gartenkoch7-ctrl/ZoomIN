using Windows.Win32;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Platform.Cursor;

/// <summary>
/// Hides the system pointer with MagShowSystemCursor (ADR-0011). Only this cursor function of the
/// Magnification API is used; the display path stays our own overlay (ADR-0002).
/// </summary>
public sealed class MagnifierSystemCursor : ISystemCursor
{
    private readonly ILogger _log;
    private readonly Lock _gate = new();
    private readonly bool _available;
    private bool _visible = true;

    public MagnifierSystemCursor(ILogger<MagnifierSystemCursor> log)
    {
        _log = log;
        _available = PInvoke.MagInitialize();
        if (!_available)
        {
            _log.MagnifierUnavailable();
        }
    }

    public void SetVisible(bool visible)
    {
        // Every render thread calls this once per frame; nearly always nothing changes. Without the
        // lock-free check each frame took the lock all monitors share (AGENTS.md 2.3, M14 step 2).
        // A stale read either takes the lock, which checks again, or skips one change that the
        // same render thread asks for again in its next frame.
        if (!_available || Volatile.Read(ref _visible) == visible)
        {
            return;
        }

        lock (_gate)
        {
            if (!_available || visible == _visible)
            {
                return;
            }

            if (PInvoke.MagShowSystemCursor(visible))
            {
                Volatile.Write(ref _visible, visible);
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
