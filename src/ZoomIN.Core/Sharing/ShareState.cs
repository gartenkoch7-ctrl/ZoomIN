using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Sharing;

/// <summary>
/// Immutable share state. Render threads read <see cref="Area"/> and <see cref="Matrix"/> per
/// frame; both are computed here, when something changes, so reading never allocates.
/// </summary>
public sealed record ShareState
{
    private readonly ShareOptions _options = ShareOptions.Default;
    private readonly SharedArea? _detected;
    private readonly PhysicalRect? _manualScreen;
    private readonly long _manualWindow;
    private readonly PhysicalRect? _manualWindowBounds;
    private readonly bool _suppressed;

    public ShareState(ShareOptions options)
    {
        Options = options;
    }

    public static ShareState Initial { get; } = new(ShareOptions.Default);

    public ShareOptions Options { get => _options; init { _options = value; Recompute(); } }

    /// <summary>Last share the source detected; null while nothing is shared.</summary>
    public SharedArea? Detected { get => _detected; init { _detected = value; Recompute(); } }

    /// <summary>Monitor bounds marked by hotkey; wins over detection.</summary>
    public PhysicalRect? ManualScreen { get => _manualScreen; init { _manualScreen = value; Recompute(); } }

    /// <summary>Window marked by hotkey (opaque id, 0 = none); its bounds follow it while it moves.</summary>
    public long ManualWindow { get => _manualWindow; init { _manualWindow = value; Recompute(); } }

    public PhysicalRect? ManualWindowBounds { get => _manualWindowBounds; init { _manualWindowBounds = value; Recompute(); } }

    /// <summary>The hotkey hid the detected share; lasts until the meeting app stops sharing.</summary>
    public bool Suppressed { get => _suppressed; init { _suppressed = value; Recompute(); } }

    /// <summary>A hotkey mark is set, even before the source reported the window's bounds.</summary>
    public bool HasManual => _manualScreen is not null || _manualWindow != 0;

    /// <summary>Where the share filter applies, in physical desktop pixels; null = nowhere.</summary>
    public PhysicalRect? Area { get; private set; }

    /// <summary>Colour matrix of the share filter.</summary>
    public ColorMatrix Matrix { get; private set; } = ColorFilters.For(ShareOptions.Default.Filter);

    // Rules (decision 2026-11-03): a hotkey mark wins over detection; detection counts only while
    // it is switched on and not hidden by the hotkey.
    private void Recompute()
    {
        Matrix = ColorFilters.For(_options.Filter);
        Area = !_options.Enabled ? null
            : _manualScreen is { } screen ? screen
            : _manualWindow != 0 ? _manualWindowBounds
            : _options.AutoDetect && !_suppressed && _detected is { } detected ? detected.Bounds
            : null;
    }
}
