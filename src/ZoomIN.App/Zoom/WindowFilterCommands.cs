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
        if (!windows.Options.Enabled || !source.TryGetForeground(out var window))
        {
            Toggled?.Invoke(null);
            return null;
        }

        return Toggle(window);
    }

    /// <summary>
    /// The window the colour filter shortcut acts on (ADR-0033): the active window, or the one under
    /// <paramref name="mouse"/> when the active one is the Windows interface (desktop, taskbar).
    /// </summary>
    public bool TryGetTarget(PhysicalPoint? mouse, out WindowFilterTarget window) => source.TryGetActive(mouse, out window);

    /// <summary>The monitor that shows most of <paramref name="window"/> (its centre).</summary>
    public string MonitorOf(WindowFilterTarget window) => monitors.MonitorAt(Center(window.Bounds));

    /// <summary>Switches the filter of <paramref name="window"/>, remembered for app + window class.</summary>
    public WindowFilterRule Toggle(WindowFilterTarget window)
    {
        var monitorFiltered = effects.CurrentFor(MonitorOf(window)).Filter.Enabled;
        return Apply(window, o => WindowFilterToggle.Next(window.Kind, window.Palette, monitorFiltered, o));
    }

    /// <summary>
    /// The colour filter shortcut on a smart monitor (ADR-0033): original colours, or back to
    /// "Automatic" so the window follows the smart inversion again; remembered for app + window class.
    /// </summary>
    public WindowFilterRule ToggleOnSmartMonitor(WindowFilterTarget window) =>
        Apply(window, o => FilterShortcutTarget.NextRule(window.Kind, window.Palette, o));

    private WindowFilterRule Apply(WindowFilterTarget window, Func<WindowFilterOptions, WindowFilterRule> next)
    {
        using var operation = Telemetry.Source.StartActivity("zoomin.windowfilter.toggle")?.SetTag("process.name", window.Kind.Process);

        // The rule is built inside the update from the options of that moment, so a change on the
        // settings page meanwhile (e.g. a deleted scheme and its automatic filter) is not undone.
        WindowFilterRule? rule = null;
        windows.UpdateOptions(o =>
        {
            rule = next(o);
            return o.WithRule(rule);
        });
        operation?.SetTag("zoomin.choice", rule!.Choice.ToString());

        Toggled?.Invoke(rule);
        return rule!;
    }

    /// <summary>Puts back the rule <paramref name="kind"/> had before a toggle (null: it had none).</summary>
    public void Restore(WindowKind kind, WindowFilterRule? previous) =>
        windows.UpdateOptions(o => previous is null ? o.WithoutRule(kind) : o.WithRule(previous));

    private static PhysicalPoint Center(PhysicalRect bounds) =>
        new(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
}
