using System.Diagnostics.CodeAnalysis;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Filters;

/// <summary>Immutable snapshot of everything that changes how pixels look; read per frame.</summary>
public sealed record DisplayEffects
{
    private static readonly IReadOnlyDictionary<string, MonitorFilter> s_noMonitors = new Dictionary<string, MonitorFilter>();

    private readonly FilterSettings _filter = FilterSettings.Default;
    private readonly IReadOnlyList<CustomScheme> _schemes = CustomSchemes.None;

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
            Recompute();
        }
    }

    /// <summary>
    /// The own schemes (M18), shared by all profiles and monitors; cleaned up and sorted by place
    /// when set. Setting it recomputes the matrices, the list order and the replacements, like <see cref="Filter"/>.
    /// </summary>
    public IReadOnlyList<CustomScheme> Schemes
    {
        get => _schemes;
        init
        {
            // The same list again (e.g. an unchanged UpdateSchemes): already clean, nothing to rebuild.
            if (ReferenceEquals(value, _schemes))
            {
                return;
            }

            _schemes = CustomSchemes.Normalized(value);
            Order = ColorFilters.Order(_schemes);
            Replacements = ReplacementTable.Build(_schemes);
            Recompute();
        }
    }

    /// <summary>Every filter in list order, own schemes last: what "next filter" cycles through.</summary>
    public IReadOnlyList<FilterId> Order { get; private set; } = ColorFilters.DisplayOrder;

    /// <summary>The replacements of all own schemes, ready for the shader.</summary>
    public ReplacementTable Replacements { get; private set; } = ReplacementTable.Empty;

    /// <summary>
    /// Place (0–3) of the own scheme the monitor shows when it has replacements, else −1; the shader
    /// applies them to pixels outside any window region (palette entry "monitor").
    /// </summary>
    public int MonitorReplaceSlot { get; private set; } = -1;

    public Smoothing Smoothing { get; init; }

    /// <summary>The monitor filter is the smart inversion (ADR-0024), decided per pixel by the shader.</summary>
    public bool IsSmart => Filter.Enabled && Filter.Filter == FilterId.InvertedSmart;

    /// <summary>Smart inversion leaves colourful, detailed surroundings (photos, videos) original.</summary>
    public bool ProtectPhotos { get; init; } = true;

    public PointerStyle Cursor { get; init; }

    /// <summary>Precomputed, so the render thread never builds matrices.</summary>
    public ColorMatrix Matrix { get; private set; } = ColorMatrix.Identity;

    /// <summary>Matrices for the filter per window, indexed like <see cref="WindowPalette"/>; entry 0 is <see cref="Matrix"/>.</summary>
    public ColorMatrix[] WindowMatrices { get; private set; } = WindowPalette.Build(FilterSettings.Default);

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

    /// <summary>Place of the own scheme whose replacements a palette entry gets, or −1. Per frame, no allocation.</summary>
    public int ReplaceSlotOf(byte entry)
    {
        var slot = entry == WindowPalette.Monitor ? MonitorReplaceSlot : WindowPalette.SchemeSlotOf(entry);
        return Replacements.CountOf(slot) > 0 ? slot : -1;
    }

    // Init accessors run in any order (constructor, with); each one recomputes from both fields.
    private void Recompute()
    {
        Matrix = ColorFilters.Build(_filter, _schemes);
        WindowMatrices = WindowPalette.Build(_filter, _schemes);
        var slot = _filter.Enabled ? CustomSchemes.SlotOf(CustomSchemes.Resolve(_filter.Filter, _schemes)) : -1;
        MonitorReplaceSlot = Replacements.CountOf(slot) > 0 ? slot : -1;
    }
}
