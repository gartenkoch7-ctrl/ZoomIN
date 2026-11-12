using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>Everything the user wants for one frame of one monitor, read from the models.</summary>
/// <param name="Windows">Window regions of the filter per window.</param>
/// <param name="WindowsFiltered">Some window on this monitor looks different from the rest (ADR-0020).</param>
/// <param name="Overview">The overview on this monitor (B2), or null. Its changes are tracked by <see cref="OverviewLayer"/>.</param>
/// <param name="OverviewLevel">The user's level, which sizes the overview frame.</param>
/// <param name="Placement">Latest "centre the view here" request for this monitor (M17), passed to the anchor.</param>
internal readonly record struct FrameRequest(
    ZoomMode Mode,
    double Zoom,
    LensSize Lens,
    DockLayout Dock,
    DisplayEffects Effects,
    long LocateRequestedAt,
    TrackingSnapshot Tracking,
    TrackingOptions TrackingOptions,
    bool PointerHidden,
    WindowRegions Windows,
    bool WindowsFiltered,
    OverviewSession? Overview = null,
    double OverviewLevel = 1,
    ViewPlacement Placement = default)
{
    /// <summary>Not magnified and no colour filter: only pointer, crosshair or locator are drawn.</summary>
    public bool OverlayOnly => Zoom <= 1.01 && !Effects.Filter.ChangesColors && !WindowsFiltered;
}
