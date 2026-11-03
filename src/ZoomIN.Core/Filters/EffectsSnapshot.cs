namespace ZoomIN.Core.Filters;

/// <summary>
/// Shared effects plus, for every monitor with its own filter, a ready-made copy with that filter
/// and its matrix. Built when settings change, so render threads only look up (no allocation).
/// </summary>
internal sealed class EffectsSnapshot
{
    private static readonly IReadOnlyDictionary<string, DisplayEffects> s_none = new Dictionary<string, DisplayEffects>();

    private readonly IReadOnlyDictionary<string, DisplayEffects> _monitors;

    private EffectsSnapshot(DisplayEffects shared, IReadOnlyDictionary<string, DisplayEffects> monitors)
    {
        Shared = shared;
        _monitors = monitors;
    }

    public DisplayEffects Shared { get; }

    public static EffectsSnapshot Create(DisplayEffects shared)
    {
        if (!shared.PerMonitorFilter || shared.MonitorFilters.Count == 0)
        {
            return new EffectsSnapshot(shared, s_none);
        }

        var monitors = new Dictionary<string, DisplayEffects>(shared.MonitorFilters.Count);
        foreach (var (id, own) in shared.MonitorFilters)
        {
            monitors[id] = shared with { Filter = shared.Filter with { Enabled = own.Enabled, Filter = own.Filter } };
        }

        return new EffectsSnapshot(shared, monitors);
    }

    public DisplayEffects For(string monitorId) =>
        _monitors.TryGetValue(monitorId, out var effects) ? effects : Shared;
}
