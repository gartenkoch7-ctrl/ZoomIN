using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// What focus and caret tracking reports about itself (ADR-0019): how long a reading takes, and
/// which caret source each application answers with. A source is logged once per process when it
/// is new – Notepad alternates between system caret and UI Automation every blink, and logging
/// every change would write two lines a second. Runs on the tracking thread, never per frame.
/// </summary>
internal sealed class TrackingTelemetry(ILogger log)
{
    // Process ids are reused; a long session meets many short-lived processes.
    private const int MaxProcesses = 64;

    private static readonly Histogram<double> s_readDuration = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.tracking.read_duration", "ms", "One reading of focus and caret; long ones mean an application answers slowly");

    private static readonly Counter<long> s_sources = Telemetry.Meter.CreateCounter<long>(
        "zoomin.tracking.caret_sources", "{source}", "A caret source seen for the first time in a process");

    private static readonly Counter<long> s_failures = Telemetry.Meter.CreateCounter<long>(
        "zoomin.tracking.read_failures", "{reading}", "Readings of focus and caret that failed unexpectedly");

    private readonly Dictionary<uint, CaretSource> _seen = [];
    private readonly HashSet<Type> _failuresLogged = [];

    /// <summary>
    /// A reading failed with an exception nobody expected. Logged once per exception type – the
    /// same window is read 30 times a second – and counted every time.
    /// </summary>
    public void ReadFailed(Exception exception)
    {
        var type = exception.GetType();
        s_failures.Add(1, new KeyValuePair<string, object?>("exception.type", type.FullName));
        if (_failuresLogged.Add(type))
        {
            log.ReadFailed(exception);
        }
    }

    public void Observe(uint processId, CaretSource source, double readMs)
    {
        s_readDuration.Record(readMs);
        if (processId == 0 || source == CaretSource.None)
        {
            return;
        }

        _seen.TryGetValue(processId, out var known);
        if ((known & source) == source)
        {
            return;
        }

        if (_seen.Count >= MaxProcesses && known == CaretSource.None)
        {
            _seen.Clear();
        }

        _seen[processId] = known | source;
        var process = ProcessName(processId);
        var name = SourceName(source);
        log.CaretSourceSeen(process, name);
        s_sources.Add(1, new KeyValuePair<string, object?>("process.name", process), new KeyValuePair<string, object?>("zoomin.caret.source", name));
    }

    private static string SourceName(CaretSource source) => source switch
    {
        CaretSource.System => "system",
        CaretSource.Msaa => "msaa",
        CaretSource.Uia => "uia",
        CaretSource.Estimated => "estimated",
        CaretSource.Cell => "cell",
        _ => "none",
    };

    // The process name only (e.g. "WINWORD"), never the window title (ADR-0019).
    private static string ProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return "ended";
        }
    }
}
