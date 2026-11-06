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

    private static readonly Counter<long> s_dropped = Telemetry.Meter.CreateCounter<long>(
        "zoomin.windowfilter.regions_dropped", "{window}", "Windows left to the monitor filter because the region limit was reached, per update");

    private bool _dropping;

    public void Classified(string process, WindowBrightness result, TimeSpan duration, int looks)
    {
        var name = ResultName(result);
        var milliseconds = Math.Round(duration.TotalMilliseconds);
        log.WindowClassified(process, name, milliseconds, looks);
        s_classifications.Add(1, new KeyValuePair<string, object?>("process.name", process), new KeyValuePair<string, object?>("zoomin.result", name));
    }

    public static void Looked(double milliseconds) => s_lookDuration.Record(milliseconds);

    public void Updated(double milliseconds, WindowRegions regions)
    {
        s_updateDuration.Record(milliseconds);
        s_regions.Record(regions.Count);
        if (regions.Dropped > 0)
        {
            s_dropped.Add(regions.Dropped);
        }

        // Logged when the limit is first hit, not on every update while it lasts (dragging).
        if ((regions.Dropped > 0) != _dropping)
        {
            _dropping = regions.Dropped > 0;
            if (_dropping)
            {
                log.WindowRegionsDropped(regions.Dropped, WindowRegions.Max);
            }
        }
    }

    private static string ResultName(WindowBrightness result) => result switch
    {
        WindowBrightness.Light => "light",
        WindowBrightness.Dark => "dark",
        _ => "unknown",
    };
}
