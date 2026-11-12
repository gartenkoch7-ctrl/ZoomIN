using Microsoft.Extensions.Logging;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.SecureDesktop.Checks;

/// <summary>
/// Everything the secure instance can check about itself (M10, Q8): the desktop it runs on, the job,
/// the watchdog and the shortcuts right at the start (Windows ends the instance without notice when
/// the desktop switches back, often within seconds), then once after the first image whether its
/// overlay is the top window at each monitor centre and excluded from capture. The overlay is
/// excluded from screen captures, so these lines replace a screenshot of the lock screen.
/// The Win32 questions run whatever the log level: their warnings must always reach the log.
/// Never throws: a failing check must not end the zoom on the sign-in screen.
/// </summary>
internal sealed class SecureDesktopChecks(ISecureDesktopProbe probe, IMonitorProvider monitors, ILogger<SecureDesktopChecks> log)
{
    // WDA_EXCLUDEFROMCAPTURE (Windows 10 2004 and later); AGENTS.md 2.4.
    internal const uint ExcludeFromCapture = 0x11;

    /// <summary>The overlays on screen: top window at the monitor centres and capture exclusion.</summary>
    public void RunOverlays()
    {
        try
        {
            CheckOverlays(probe.OwnOverlays());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.ChecksFailed(ex);
        }
    }

    /// <summary>What needs no image: desktop, job, watchdog, shortcuts.</summary>
    public void RunAtStart(SecureCheckInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        try
        {
            var desktop = probe.DesktopName() ?? "unknown";
            log.CheckDesktop(desktop);
            var inJob = Describe(probe.IsInJob());
            log.CheckJob(inJob);
            CheckWatchdog(inputs.WatchdogId);
            foreach (var gesture in inputs.Unavailable)
            {
                log.CheckShortcutUnavailable(gesture);
            }

            log.CheckShortcuts(inputs.Unavailable.Count, inputs.ExitShortcut, inputs.SnapshotShortcut);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.ChecksFailed(ex);
        }
    }

    private void CheckOverlays(IReadOnlyList<ProbedWindow> overlays)
    {
        if (overlays.Count == 0)
        {
            log.CheckNoOverlay();
            return;
        }

        foreach (var monitor in monitors.MonitorIds())
        {
            if (monitors.BoundsOf(monitor) is not { } bounds)
            {
                continue;
            }

            var centre = new PhysicalPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);

            // A monitor without zoom has no overlay of its own; whatever is on top there is fine.
            if (!overlays.Any(o => o.Bounds.Contains(centre)))
            {
                continue;
            }

            var top = probe.TopWindowAt(centre);
            if (top is { } window && overlays.Any(o => o.Handle == window.Handle))
            {
                log.CheckOnTop(monitor);
            }
            else
            {
                log.CheckNotOnTop(monitor, top?.ClassName ?? "none");
            }
        }

        foreach (var overlay in overlays)
        {
            var affinity = probe.DisplayAffinity(overlay.Handle);
            if (affinity == ExcludeFromCapture)
            {
                log.CheckAffinity(overlay.Handle);
            }
            else
            {
                log.CheckAffinityFailed(overlay.Handle, affinity is { } value ? $"0x{value:X}" : "unknown");
            }
        }
    }

    private void CheckWatchdog(int? watchdogId)
    {
        if (watchdogId is not { } id)
        {
            log.CheckNoWatchdog();
            return;
        }

        var watchdog = probe.Process(id);
        var inJob = Describe(watchdog.InJob);
        if (watchdog.Alive)
        {
            log.CheckWatchdog(id, inJob);
        }
        else
        {
            log.CheckWatchdogGone(id, inJob);
        }
    }

    private static string Describe(bool? value) => value switch
    {
        true => "yes",
        false => "no",
        null => "unknown",
    };
}
