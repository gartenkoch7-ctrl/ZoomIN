using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.App.Zoom;

/// <summary>
/// The colour filter shortcut (Ctrl+Alt+C by default, ADR-0033). With the smart inversion on, one press switches
/// the filter of the active window only (remembered per app), two quick presses switch the whole
/// monitor; every other filter switches the monitor under the mouse as before. Called on the hotkey
/// thread only, so the remembered first press needs no lock.
/// </summary>
public sealed class FilterShortcut(
    EffectsModel effects, WindowFilterModel windows, WindowFilterCommands windowFilter, IMonitorProvider monitors,
    ICursorSource cursor, TimeProvider time)
{
    private FirstPress? _first;

    /// <summary>What the last press did: "window", "monitor" or "double" (for tests and telemetry).</summary>
    public string LastTarget { get; private set; } = "";

    public void Press()
    {
        var now = time.GetTimestamp();
        var first = _first;
        _first = null;
        using var operation = Telemetry.Source.StartActivity("zoomin.filter.shortcut");
        if (first is not null && FilterShortcutTarget.IsDoublePress(time.GetElapsedTime(first.At, now)))
        {
            // The first press switched the window only: take that back, then switch the whole monitor.
            windowFilter.Restore(first.Kind, first.Previous);
            effects.ToggleFilter(first.Monitor);
            Done(operation, "double");
            return;
        }

        PhysicalPoint? mouse = cursor.TryGetPosition(out var position) ? position : null;
        if (windowFilter.TryGetTarget(mouse, out var window))
        {
            var monitor = windowFilter.MonitorOf(window);
            if (FilterShortcutTarget.ActsOnWindow(effects.CurrentFor(monitor).IsSmart, windows.Options.Enabled))
            {
                var previous = windows.Options.RuleFor(window.Kind);
                windowFilter.ToggleOnSmartMonitor(window);
                _first = new FirstPress(now, window.Kind, previous, monitor);
                Done(operation, "window");
                return;
            }
        }

        effects.ToggleFilter(monitors.MonitorAt(mouse ?? default));
        Done(operation, "monitor");
    }

    private void Done(System.Diagnostics.Activity? operation, string target)
    {
        LastTarget = target;
        operation?.SetTag("zoomin.target", target);
    }

    private sealed record FirstPress(long At, WindowKind Kind, WindowFilterRule? Previous, string Monitor);
}
