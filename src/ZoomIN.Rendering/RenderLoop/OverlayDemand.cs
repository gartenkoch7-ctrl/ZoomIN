using System.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Whether one monitor needs its overlay although it is not zoomed: filters, pointer options and
/// the locator work at 1× too (M5), so does the overview frame (B2). Per frame: lock-free, 0 B.
/// </summary>
internal sealed class OverlayDemand
{
    private readonly string _deviceName;
    private readonly RenderModels _models;
    private readonly ZoomModel _zoom;
    private readonly EffectsModel _effects;

    public OverlayDemand(string deviceName, RenderModels models)
    {
        _deviceName = deviceName;
        _models = models;
        _zoom = models.Zoom;
        _effects = models.Effects;
    }

    /// <summary>The overview on this monitor, or null.</summary>
    public OverviewSession? Overview => _models.Overview.Current is { } running && running.MonitorId == _deviceName ? running : null;

    /// <summary>Colour filter, a pointer drawn by ZoomIN or a pointer hidden while typing.</summary>
    public bool NeedsOverlayUnzoomed(DisplayEffects effects) =>
        effects.Filter.ChangesColors || effects.Cursor.NeedsOverlayUnzoomed || _effects.PointerHiddenByTyping;

    /// <summary>A locator request keeps an otherwise idle monitor rendering until the ring is gone.</summary>
    public bool LocatorRequested(DisplayEffects effects)
    {
        var at = _effects.LocateRequestedAt;
        return at != 0 && Stopwatch.GetElapsedTime(at).TotalSeconds < effects.Cursor.LocatorSeconds;
    }

    /// <summary>
    /// What the monitor shows (as <see cref="ZoomModel.EffectiveTargetFor"/>) and whether a temporary
    /// level (overview, window drag) holds it below the user's level, including the glide there.
    /// Ceiling and target come from one read each, so the animator target, the anchor mode and the
    /// kept view of a frame agree.
    /// </summary>
    public double EffectiveTarget(out bool lowered)
    {
        // Ceiling first, as in EffectiveTargetFor: commands publish targets before clearing ceilings.
        var capped = _zoom.Temporary.TryGetCeiling(_deviceName, out var ceiling);
        var target = _zoom.TargetFor(_deviceName);
        lowered = capped && ceiling < target;
        return lowered ? ceiling : target;
    }

    /// <summary>
    /// The mode the anchor follows. Unzoomed frames are drawn as Fullscreen, but a temporary 1×
    /// (overview, window drag) is no mode change: the anchor keeps the user's mode, so the view
    /// returns where it was and a placed view is computed for the mode the zoom glides back to.
    /// </summary>
    public ZoomMode AnchorMode(ZoomMode frameMode, bool unzoomed, OverviewSession? overview, bool lowered) =>
        unzoomed && (overview is not null || lowered) ? _zoom.Mode : frameMode;

    /// <summary>After a pipeline failure: retry soon if the monitor should show anything, else wait for a change.</summary>
    public bool NeededAfterFailure() =>
        _zoom.EffectiveTargetFor(_deviceName) > ZoomLimits.Min || NeedsOverlayUnzoomed(_effects.CurrentFor(_deviceName))
        || _models.Windows.Regions.Count > 0 || Overview is not null;
}
