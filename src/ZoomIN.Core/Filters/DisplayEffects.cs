using System.Diagnostics.CodeAnalysis;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Filters;

/// <summary>Immutable snapshot of everything that changes how pixels look; read per frame.</summary>
public sealed record DisplayEffects
{
    private static readonly IReadOnlyDictionary<string, MonitorFilter> s_noMonitors = new Dictionary<string, MonitorFilter>();

    private readonly FilterSettings _filter = FilterSettings.Default;

    public DisplayEffects(FilterSettings filter, Smoothing smoothing, PointerStyle cursor)
    {
        Filter = filter;
        Smoothing = smoothing;
        Cursor = cursor;
    }

    public static DisplayEffects Default { get; } = new(FilterSettings.Default, Smoothing.Text, PointerStyle.Default);

    /// <summary>
    /// Shared filter: used by every monitor without an own entry. Setting it (also via <c>with</c>)
    /// recomputes <see cref="Matrix"/>; a plain auto-property would copy the old matrix in <c>with</c> expressions.
    /// </summary>
    public FilterSettings Filter
    {
        get => _filter;
        init
        {
            _filter = value;
            Matrix = ColorFilters.Build(value);
        }
    }

    public Smoothing Smoothing { get; init; }

    public PointerStyle Cursor { get; init; }

    /// <summary>Precomputed, so the render thread never builds matrices.</summary>
    public ColorMatrix Matrix { get; private init; } = ColorMatrix.Identity;

    /// <summary>Monitors may have their own filter (on by default, like the zoom level).</summary>
    public bool PerMonitorFilter { get; init; } = true;

    /// <summary>Own filter per monitor device name, e.g. "\\.\DISPLAY1"; used only with <see cref="PerMonitorFilter"/>.</summary>
    public IReadOnlyDictionary<string, MonitorFilter> MonitorFilters { get; init; } = s_noMonitors;

    /// <summary>Filter that applies to the monitor: its own entry, or the shared one.</summary>
    public MonitorFilter FilterOf(string monitorId) =>
        HasOwnFilter(monitorId, out var own) ? own : new MonitorFilter(Filter.Enabled, Filter.Filter);

    public bool HasOwnFilter(string monitorId, [NotNullWhen(true)] out MonitorFilter? filter)
    {
        filter = null;
        return PerMonitorFilter && MonitorFilters.TryGetValue(monitorId, out filter);
    }
}
