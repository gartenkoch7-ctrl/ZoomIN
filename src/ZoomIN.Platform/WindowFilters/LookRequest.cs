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
/// <param name="TitleBarBottom">Desktop y where its title bar ends; at or above the top of <paramref name="Area"/> (default: −∞) if none – 0 is a real y on monitors above the main one.</param>
internal readonly record struct LookRequest(
    nint Handle, PhysicalRect Area, PhysicalRect[] Above, PhysicalRect[] Monitors, int Threshold, double TitleBarBottom = double.NegativeInfinity)
{
    /// <summary>Copies the area once and counts page and title bar apart; null where nothing could be seen.</summary>
    public LookResult Take(ScreenSampler? sampler)
    {
        if (sampler is null)
        {
            return new LookResult(Handle, null);
        }

        var started = Stopwatch.GetTimestamp();
        var result = new LookResult(Handle, null);
        if (!RectMath.IsEmpty(Area) && sampler.Capture(Area))
        {
            var (page, titleBar) = BrightnessMeter.IsLightApart(
                sampler.Pixels, ScreenSampler.Size, ScreenSampler.Rows, Area, TitleBarBottom, Above, Monitors, Threshold);
            result = new LookResult(Handle, page, titleBar);
        }

        WindowFilterTelemetry.Looked(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return result;
    }
}
