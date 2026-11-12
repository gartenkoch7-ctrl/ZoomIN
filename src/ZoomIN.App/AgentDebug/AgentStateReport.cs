using System.Globalization;
using System.Reflection;
using System.Text;
using ZoomIN.App.Overview;
using ZoomIN.App.Zoom;
using ZoomIN.Core.AgentDebug;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.AgentDebug;

/// <summary>
/// The text of <see cref="AgentTools.GetState"/> (ADR-0027): what ZoomIN does right now. Only
/// technical values – process names, window classes, rectangles – never titles (AGENTS.md 2.5).
/// </summary>
public sealed class AgentStateReport(
    ZoomModel zoom, EffectsModel effects, WindowFilterModel windows, WindowFilterCommands windowFilter,
    IWindowFilterSource windowSource, IMonitorProvider monitors, OverviewDebug overview)
{
    public string Build()
    {
        var text = new StringBuilder();
        var version = typeof(AgentStateReport).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
        Line(text, $"ZoomIN {version}, KI-Debug protocol {AgentProtocol.Version}");
        Line(text, $"Zoom: {(zoom.IsOn ? "on" : "off")}, mode {zoom.Mode}");

        Line(text, "Monitors:");
        foreach (var id in monitors.MonitorIds())
        {
            var filter = effects.CurrentFor(id).Filter;
            var bounds = monitors.BoundsOf(id) is { } b ? RectText.Format(b) : "(unknown)";
            // A temporary level (overview, window drag) caps what the monitor shows (M17). One read
            // of target and ceiling each, so the line never contradicts itself.
            var target = zoom.TargetFor(id);
            var temporary = zoom.Temporary.TryGetCeiling(id, out var ceiling)
                ? string.Create(CultureInfo.InvariantCulture, $" (temporary {ceiling:0.00}, shows {Math.Min(target, ceiling):0.00})")
                : string.Empty;

            // Frozen (G2): its target is parked; it shows its own level, mode and image, temporary levels pass it by.
            if (zoom.Frozen.For(id) is { } frozen)
            {
                var ignored = temporary.Length > 0 ? string.Create(CultureInfo.InvariantCulture, $", temporary {ceiling:0.00} ignored") : string.Empty;
                temporary = string.Create(CultureInfo.InvariantCulture, $" (frozen: shows {frozen.Level:0.00} {frozen.Mode}, {frozen.ImageKind}{ignored})");
            }
            Line(text, string.Create(CultureInfo.InvariantCulture, $"  {id} {bounds}: zoom {target:0.00}{temporary}, monitor filter {(filter.Enabled ? filter.Filter.ToString() : "off")}"));
        }

        Line(text, overview.Describe());
        var options = windows.Options;
        Line(text, $"Filter per window: {(options.Enabled ? "on" : "off")}, automatic filter {options.AutoFilter}, "
            + $"sensitivity {options.Sensitivity} (light from {options.ThresholdPercent} %)");
        Line(text, $"Remembered rules: {options.Rules.Count}");
        foreach (var rule in options.Rules)
        {
            var filter = rule.Choice == WindowFilterChoice.Filter ? " " + rule.Filter : string.Empty;
            Line(text, $"  {rule.Process} / {rule.WindowClass} -> {rule.Choice}{filter}, title bar {rule.TitleBar}");
        }

        var regions = windows.Regions;
        Line(text, $"Window regions now (front to back): {regions.Count}, dropped {regions.Dropped}");
        for (var i = 0; i < regions.Count; i++)
        {
            Line(text, $"  {i,2}: {RectText.Format(regions.Rects[i])} -> {WindowPalette.Name(regions.Palette[i])}");
        }

        Line(text, windowSource.TryGetForeground(out var foreground)
            ? $"Foreground window: {foreground.Kind.Process} / {foreground.Kind.WindowClass} {RectText.Format(foreground.Bounds)} -> {WindowPalette.Name(foreground.Palette)}"
            : "Foreground window: none the filter acts on");

        Line(text, "Open window kinds (app / class): " + string.Join(", ", windowFilter.OpenWindowKinds().Select(k => $"{k.Process} / {k.WindowClass}")));
        return text.ToString();
    }

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');
}
