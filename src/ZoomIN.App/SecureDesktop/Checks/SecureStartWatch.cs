using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.SecureDesktop.Checks;

/// <summary>
/// Follows the start of the secure instance to the first magnified image (M10, Q3). The saved
/// level applies at once instead of gliding in from 1×; the pipeline, the first desktop copy, the
/// first shown frame and the zoom level reached become child operations of "zoomin.secure.start"
/// and one log line. <see cref="SecureDesktopChecks"/> runs at the start and, for the overlays, after the first image.
/// </summary>
internal sealed class SecureStartWatch(RenderModels models, SecureDesktopChecks checks, TimeProvider time, ILogger<SecureStartWatch> log)
{
    /// <summary>Acceptance criterion of M10 is 1 s; after this long there is no first image to wait for.</summary>
    internal static readonly TimeSpan FirstImageTimeout = TimeSpan.FromSeconds(10);

    // Only during the start, at most 10 s: a few hundred cheap reads, no signal into the render loop.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    // Pipelines of all monitors start together and take ~100–300 ms each (measured 2026-11-12).
    private static readonly TimeSpan OtherMonitorsGrace = TimeSpan.FromSeconds(1);

    private double? _animationSeconds;
    private long _engineStart;

    /// <summary>Before the engine starts (it builds the pipelines at once): the render threads record the milestones.</summary>
    public void Begin()
    {
        _engineStart = models.Milestones.Time.GetTimestamp();
        models.Milestones.Arm();
    }

    /// <summary>
    /// Before switching the zoom on: the first frames show the saved level, not 1×. The glide is the
    /// user's setting; it comes back with the first image at the level (at the latest after 10 s).
    /// </summary>
    public void SkipZoomAnimation()
    {
        _animationSeconds = models.Zoom.AnimationSeconds;
        models.Zoom.AnimationSeconds = 0;
    }

    /// <param name="processStart">When this process started (UTC), the base of every logged time.</param>
    /// <param name="start">"zoomin.secure.start" while the zoom starts on, ended here; otherwise null.</param>
    /// <param name="zoomOn">The zoom starts on (after <see cref="SkipZoomAnimation"/>); otherwise only a filter may show an image.</param>
    /// <param name="exiting">The instance ends: stop waiting, report and check nothing more.</param>
    public async Task FollowAsync(DateTimeOffset processStart, Activity? start, bool zoomOn, SecureCheckInputs inputs, CancellationToken exiting)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var milestones = models.Milestones;
        checks.RunAtStart(inputs);
        try
        {
            using (start)
            {
                var waitStart = time.GetTimestamp();
                try
                {
                    while ((milestones.FirstShown == 0 || (zoomOn && milestones.ZoomReached == 0)) && time.GetElapsedTime(waitStart) <= FirstImageTimeout)
                    {
                        await Task.Delay(PollInterval, time, exiting).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    start?.SetStatus(ActivityStatusCode.Error, "ended before the first image");
                    throw;
                }

                Report(processStart, start, zoomOn);
            }

            // The milestone comes from the fastest monitor; the others build their pipelines in
            // parallel and should not glide in from 1× either.
            if (zoomOn && milestones.ZoomReached != 0)
            {
                await Task.Delay(OtherMonitorsGrace, time, exiting).ConfigureAwait(false);
            }

            RestoreAnimation();
            checks.RunOverlays();
        }
        catch (OperationCanceledException)
        {
            // Ended during the start (a short UAC prompt, the exit shortcut): no timings, but the
            // checks still – most of them need no image, and short sessions are the ones to explain.
            checks.RunOverlays();
        }
        finally
        {
            RestoreAnimation();

            // A start without zoom never reaches the level: stop the per-frame checks either way.
            milestones.Disarm();
        }
    }

    private void Report(DateTimeOffset processStart, Activity? start, bool zoomOn)
    {
        // A filter at 1× is no magnified image: the start timings are about the zoom only.
        var milestones = models.Milestones;
        if (!zoomOn)
        {
            return;
        }

        if (milestones.FirstShown == 0)
        {
            log.NoFirstImage(FirstImageTimeout.TotalSeconds);
            start?.SetStatus(ActivityStatusCode.Error, "no magnified image");
            return;
        }

        // One clock for wall time and timestamps: the one the render threads stamp with.
        var clock = milestones.Time;
        var now = clock.GetUtcNow();
        var nowStamp = clock.GetTimestamp();
        DateTimeOffset At(long stamp) => now - clock.GetElapsedTime(stamp, nowStamp);
        double SinceStart(long stamp) => (At(stamp) - processStart).TotalMilliseconds;

        var shownMs = SinceStart(milestones.FirstShown);
        log.FirstImage(shownMs);

        if (milestones.ZoomReached == 0)
        {
            // An image, but never at the saved level: the start is not what M10 promises.
            log.ZoomLevelNotReached(FirstImageTimeout.TotalSeconds);
            start?.SetStatus(ActivityStatusCode.Error, "zoom level not reached");
            return;
        }

        var pipelineMs = SinceStart(milestones.PipelineReady);
        var captureMs = SinceStart(milestones.FirstCapture);
        var zoomMs = SinceStart(milestones.ZoomReached);
        log.StartTimings(pipelineMs, captureMs, shownMs, zoomMs);
        if (start is null)
        {
            return;
        }

        // Back to back: engine start → pipeline built → first copy (waiting for Desktop Duplication)
        // → first frame on screen → zoom level. The copy is stamped after its frame, so "present"
        // is short; a long "capture" means the duplication delivered late.
        var pipeline = At(milestones.PipelineReady);
        var capture = At(milestones.FirstCapture);
        var shown = At(milestones.FirstShown);
        Child(start, "zoomin.secure.start.pipeline", At(_engineStart), pipeline);
        Child(start, "zoomin.secure.start.capture", pipeline, capture);
        Child(start, "zoomin.secure.start.present", capture, shown);
        Child(start, "zoomin.secure.start.zoom_in", shown, At(milestones.ZoomReached));
    }

    private static void Child(Activity parent, string name, DateTimeOffset from, DateTimeOffset to)
    {
        // At least one tick: Activity.Stop replaces a zero duration with "now".
        using var child = Telemetry.Source.StartActivity(name, ActivityKind.Internal, parent.Context, startTime: from);
        child?.SetEndTime((to > from ? to : from.AddTicks(1)).UtcDateTime);
    }

    // The user's own glide applies again to every later change of the level; only once.
    private void RestoreAnimation()
    {
        if (_animationSeconds is { } seconds && models.Zoom.AnimationSeconds == 0)
        {
            models.Zoom.AnimationSeconds = seconds;
        }

        _animationSeconds = null;
    }
}
