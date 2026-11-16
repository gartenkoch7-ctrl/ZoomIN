using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Gives log entries written after the fact (made before the pipeline existed) the trace and span
/// id of the operation that was running when they were made. Setting that operation as
/// <see cref="Activity.Current"/> again is not possible once it has ended, so the ids are set on
/// the record directly, before the batch processor takes it.
/// </summary>
internal sealed class ReplayContextProcessor : BaseProcessor<LogRecord>
{
    [ThreadStatic]
    private static ActivityContext t_context;

    /// <summary>Entries logged on this thread until disposal get <paramref name="context"/>.</summary>
    public static Scope Use(ActivityContext context)
    {
        t_context = context;
        return default;
    }

    public override void OnEnd(LogRecord data)
    {
        if (t_context != default)
        {
            data.TraceId = t_context.TraceId;
            data.SpanId = t_context.SpanId;
            data.TraceFlags = t_context.TraceFlags;
        }
    }

    public readonly struct Scope : IDisposable
    {
        public void Dispose() => t_context = default;
    }
}
