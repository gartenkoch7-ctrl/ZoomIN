using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// What the filter per window reports about itself (ADR-0019/0020): measurement results per
/// program, how long looks and updates take, and how many regions the shader gets. Runs on the
/// tracker thread, never per frame. Program file names only, never window titles.
/// </summary>
internal sealed class WindowFilterTelemetry(ILogger log)
{
    private static readonly Counter<long> s_classifications = Telemetry.Meter.CreateCounter<long>(
        "zoomin.windowfilter.classifications", "{window}", "Windows measured when they opened, by result");

    private static readonly Histogram<double> s_lookDuration = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.windowfilter.look_duration", "ms", "One brightness look (screen copy and count)");

    private static readonly Histogram<double> s_updateDuration = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.windowfilter.update_duration", "ms", "Listing the windows and building the regions after a change");

    private static readonly Histogram<long> s_regions = Telemetry.Meter.CreateHistogram<long>(
        "zoomin.windowfilter.regions", "{region}", "Regions handed to the shader after a change");

    public void Classified(string process, WindowBrightness result, TimeSpan duration, int looks)
    {
        var name = ResultName(result);
        var milliseconds = Math.Round(duration.TotalMilliseconds);
        log.WindowClassified(process, name, milliseconds, looks);
        s_classifications.Add(1, new KeyValuePair<string, object?>("process.name", process), new KeyValuePair<string, object?>("zoomin.result", name));
    }

    public static void Looked(double milliseconds) => s_lookDuration.Record(milliseconds);

    public static void Updated(double milliseconds, int regions)
    {
        s_updateDuration.Record(milliseconds);
        s_regions.Record(regions);
    }

    private static string ResultName(WindowBrightness result) => result switch
    {
        WindowBrightness.Light => "light",
        WindowBrightness.Dark => "dark",
        _ => "unknown",
    };
}
