using Microsoft.Extensions.Logging;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Platform.WindowFilters;

namespace ZoomIN.Platform.WindowDrag;

/// <summary>
/// Writes the start of a drag (2900, and 2902/2903 if the hit test was unclear). Reads program
/// and class of the window, never its title, and only while Debug logging is on.
/// </summary>
internal sealed class DragStartReport(ILogger log)
{
    public void Started(nint window, uint process, WindowDragKind kind, DragStartProbe probe)
    {
        if (!log.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        // Read once per drag. A fresh WindowNames on purpose: its process cache is never pruned
        // here, and reused process ids would keep a stale name.
        var names = new WindowNames().KindOf(window, process);
        log.DragStarted(names.Process, names.WindowClass, kind);
        if (probe.HitKind == WindowDragKind.Unknown)
        {
            log.KindUnclear(probe.HitTest);
            if (kind != WindowDragKind.Unknown)
            {
                log.KindResolved(kind); // outline dragging has no size change to tell it
            }
        }
    }
}
