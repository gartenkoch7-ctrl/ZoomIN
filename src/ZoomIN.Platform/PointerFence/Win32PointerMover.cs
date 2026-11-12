using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.PointerFence;

/// <summary>SetCursorPos on an explicit command only (ADR-0028); every move is logged with reason and monitor.</summary>
public sealed class Win32PointerMover : IPointerMover
{
    private readonly ILogger _log;
    private readonly IMonitorProvider _monitors;
    private readonly Func<int, int, (bool Moved, int Error)> _setPosition;

    public Win32PointerMover(ILogger<Win32PointerMover> log, IMonitorProvider monitors)
        : this(log, monitors, SetCursorPosition)
    {
    }

    internal Win32PointerMover(ILogger log, IMonitorProvider monitors, Func<int, int, (bool Moved, int Error)> setPosition)
    {
        _log = log;
        _monitors = monitors;
        _setPosition = setPosition;
    }

    public bool MoveTo(PhysicalPoint target, PointerMoveReason reason)
    {
        using var activity = Telemetry.Source.StartActivity("zoomin.pointer.move");
        var monitor = _monitors.MonitorAt(target);
        activity?.SetTag("zoomin.reason", reason.ToString());
        activity?.SetTag("zoomin.monitor", monitor);

        var (moved, error) = _setPosition((int)Math.Round(target.X), (int)Math.Round(target.Y));
        if (moved)
        {
            _log.PointerMoved(monitor, reason);
            return true;
        }

        activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, "refused");
        _log.MoveFailed(reason, error);
        return false;
    }

    private static (bool Moved, int Error) SetCursorPosition(int x, int y) =>
        PInvoke.SetCursorPos(x, y) ? (true, 0) : (false, Marshal.GetLastPInvokeError());
}
