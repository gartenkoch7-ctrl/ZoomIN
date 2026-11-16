using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Pointer;

/// <summary>
/// Measurements of mouse buffer and axis lock (A6, docs/kb/telemetrie.md). The filters run in the
/// low-level mouse hook, so they only increment numbers; the SDK reads them through observable
/// counters, and the hook path stays at 0 B even with a listener.
/// </summary>
internal static class PointerConstraintMetrics
{
    private static readonly KeyValuePair<string, object?> s_horizontal = new("zoomin.axis", "horizontal");
    private static readonly KeyValuePair<string, object?> s_vertical = new("zoomin.axis", "vertical");

    private static long s_bufferBlocked;
    private static long s_horizontalLocks;
    private static long s_verticalLocks;

    private static readonly ObservableCounter<long> s_bufferCounter = Telemetry.Meter.CreateObservableCounter(
        "zoomin.pointer.buffer_blocked", static () => Volatile.Read(ref s_bufferBlocked), unit: "{press}",
        description: "Presses against a monitor edge the mouse buffer held back (each start of a press)");

    private static readonly ObservableCounter<long> s_axisCounter = Telemetry.Meter.CreateObservableCounter<long>(
        "zoomin.pointer.axis_lock", static () => AxisMeasurements(), unit: "{lock}",
        description: "Axis lock uses: the held key chose an axis");

    public static long BufferBlocked => Volatile.Read(ref s_bufferBlocked);

    public static long AxisLocks(LockAxis axis) =>
        axis == LockAxis.Vertical ? Volatile.Read(ref s_verticalLocks) : Volatile.Read(ref s_horizontalLocks);

    /// <summary>
    /// Runs the type initializer off the hook: the filters call it when they are created, so the
    /// instruments exist from the start and the hook never creates them (allocation, SDK locks).
    /// </summary>
    public static void EnsureCreated()
    {
        GC.KeepAlive(s_bufferCounter);
        GC.KeepAlive(s_axisCounter);
    }

    /// <summary>From the mouse hook: no allocation.</summary>
    public static void Blocked() => Interlocked.Increment(ref s_bufferBlocked);

    /// <summary>From the mouse hook: no allocation.</summary>
    public static void Locked(LockAxis axis)
    {
        if (axis == LockAxis.Vertical)
        {
            Interlocked.Increment(ref s_verticalLocks);
        }
        else
        {
            Interlocked.Increment(ref s_horizontalLocks);
        }
    }

    // Read by the SDK on its export, never in the hook; the static fields root the instruments.
    private static IEnumerable<Measurement<long>> AxisMeasurements()
    {
        return
        [
            new(Volatile.Read(ref s_horizontalLocks), s_horizontal),
            new(Volatile.Read(ref s_verticalLocks), s_vertical),
        ];
    }
}
