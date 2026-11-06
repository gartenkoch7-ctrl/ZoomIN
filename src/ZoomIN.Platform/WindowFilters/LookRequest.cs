using System.Diagnostics;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// One brightness look at a window, prepared on the tracker thread with everything it needs, so
/// it can be taken on another thread (<see cref="LookWorker"/>): a GDI copy of the screen waits
/// for the next DWM frame (≈ 17 ms at 60 Hz), and the tracker must not wait with it.
/// </summary>
/// <param name="Handle">The window looked at.</param>
/// <param name="Area">Its frame, clipped to the monitors (GDI delivers black outside).</param>
/// <param name="Above">Windows above it at the time of the request.</param>
/// <param name="Monitors">Monitor bounds.</param>
/// <param name="Threshold">Light share in % from which the window counts as light.</param>
internal readonly record struct LookRequest(nint Handle, PhysicalRect Area, PhysicalRect[] Above, PhysicalRect[] Monitors, int Threshold)
{
    /// <summary>Copies the area and counts; null if it could not be seen.</summary>
    public bool? Take(ScreenSampler? sampler)
    {
        if (sampler is null)
        {
            return null;
        }

        var started = Stopwatch.GetTimestamp();
        bool? result = null;
        if (!RectMath.IsEmpty(Area) && sampler.Capture(Area))
        {
            result = BrightnessMeter.IsLight(sampler.Pixels, ScreenSampler.Size, ScreenSampler.Size, Area, Above, Monitors, Threshold);
        }

        WindowFilterTelemetry.Looked(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return result;
    }
}
