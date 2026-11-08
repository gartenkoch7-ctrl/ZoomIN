using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.App.Zoom;

/// <summary>The shortcut "filter for the active window on/off" and the open windows for the detail page (ADR-0020).</summary>
public sealed class WindowFilterCommands(
    WindowFilterModel windows, EffectsModel effects, IWindowFilterSource source, IMonitorProvider monitors)
{
    /// <summary>After the shortcut: the new rule, or null if nothing changed. On the hotkey thread.</summary>
    public event Action<WindowFilterRule?>? Toggled;

    /// <summary>App + class of the windows open now, for the detail page of the filter per window.</summary>
    public IReadOnlyList<WindowKind> OpenWindowKinds() => source.OpenKinds();

    /// <summary>
    /// Remembered for app + window class. Returns the new rule, or null if there is no window the
    /// filter acts on (or it is off).
    /// </summary>
    public WindowFilterRule? Toggle()
    {
        var options = windows.Options;
        if (!options.Enabled || !source.TryGetForeground(out var window))
        {
            Toggled?.Invoke(null);
            return null;
        }

        var center = new PhysicalPoint(window.Bounds.X + (window.Bounds.Width / 2), window.Bounds.Y + (window.Bounds.Height / 2));
        var monitorFiltered = effects.CurrentFor(monitors.MonitorAt(center)).Filter.Enabled;
        var rule = WindowFilterToggle.Next(window.Kind, window.Palette, monitorFiltered, options);
        using (Telemetry.Source.StartActivity("zoomin.windowfilter.toggle")?
            .SetTag("process.name", window.Kind.Process)
            .SetTag("zoomin.choice", rule.Choice.ToString()))
        {
            windows.SetOptions(options.WithRule(rule));
        }

        Toggled?.Invoke(rule);
        return rule;
    }
}
