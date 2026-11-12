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
    private readonly Func<PhysicalPoint, (bool Moved, int Error)> _setPosition;

    public Win32PointerMover(ILogger<Win32PointerMover> log, IMonitorProvider monitors)
        : this(log, monitors, SetCursorPosition)
    {
    }

    internal Win32PointerMover(ILogger log, IMonitorProvider monitors, Func<PhysicalPoint, (bool Moved, int Error)> setPosition)
    {
        _log = log;
        _monitors = monitors;
        _setPosition = setPosition;
    }

    public bool MoveTo(PhysicalPoint target, PointerMoveReason reason)
    {
        using var activity = Telemetry.Source.StartActivity("zoomin.pointer.move");

        // SetCursorPos takes whole physical pixels (the process is per-monitor DPI aware v2). Floor,
        // as in the fence hook: rounding up could land on the next (maybe just frozen) monitor.
        var pixel = new PhysicalPoint(Math.Floor(target.X), Math.Floor(target.Y));
        var monitor = _monitors.MonitorAt(pixel);
        activity?.SetTag("zoomin.reason", reason.ToString());
        activity?.SetTag("zoomin.monitor", monitor);
        var (moved, error) = _setPosition(pixel);
        if (moved)
        {
            _log.PointerMoved(monitor, reason);
            return true;
        }

        activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, "refused");
        _log.MoveFailed(reason, error);
        return false;
    }

    private static (bool Moved, int Error) SetCursorPosition(PhysicalPoint point) =>
        PInvoke.SetCursorPos((int)point.X, (int)point.Y) ? (true, 0) : (false, Marshal.GetLastPInvokeError());
}
