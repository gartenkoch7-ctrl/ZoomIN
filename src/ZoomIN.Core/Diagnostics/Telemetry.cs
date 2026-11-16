using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// The one activity source and the one meter of ZoomIN (ADR-0019). Both are part of .NET and cost
/// nothing while nobody listens; Platform's telemetry host decides what is recorded. Names follow
/// <c>zoomin.&lt;feature&gt;.&lt;operation&gt;</c> and are listed in docs/kb/telemetrie.md.
/// </summary>
public static class Telemetry
{
    public const string Name = "ZoomIN";

    /// <summary>Attribute naming the process role, e.g. "app" or "watchdog".</summary>
    public const string RoleAttribute = "zoomin.role";

    public static ActivitySource Source { get; } = new(Name);

    public static Meter Meter { get; } = new(Name);

    private static Action<TimeSpan>? s_flush;

    /// <summary>Set by the telemetry host: writes what is still buffered.</summary>
    public static void OnFlush(Action<TimeSpan>? flush) => Volatile.Write(ref s_flush, flush);

    /// <summary>
    /// Call before ending the process hard (<see cref="Environment.FailFast(string)"/>, crash
    /// handlers): logs are written in batches and would otherwise lose the last, most useful lines.
    /// </summary>
    public static void FlushBeforeExit() => Volatile.Read(ref s_flush)?.Invoke(TimeSpan.FromSeconds(1));
}
