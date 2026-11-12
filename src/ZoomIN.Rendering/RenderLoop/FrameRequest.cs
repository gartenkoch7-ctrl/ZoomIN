using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>Everything the user wants for one frame of one monitor, read from the models.</summary>
/// <param name="Mode">Mode of the frame geometry; Fullscreen while unzoomed (the whole monitor).</param>
/// <param name="AnchorMode">Mode the anchor follows: the user's mode during an overview (B2), so its 1× is no mode change
/// and a placed view is computed for the mode the zoom returns to; otherwise <paramref name="Mode"/>.</param>
/// <param name="Windows">Window regions of the filter per window.</param>
/// <param name="WindowsFiltered">Some window on this monitor looks different from the rest (ADR-0020).</param>
/// <param name="Overview">The overview on this monitor (B2), or null. Its changes are tracked by <see cref="OverviewLayer"/>.</param>
/// <param name="OverviewLevel">The user's level, which sizes the overview frame.</param>
/// <param name="Placement">Latest "centre the view here" request for this monitor (M17), passed to the anchor.</param>
/// <param name="ViewHeld">A temporary level (window drag) holds the monitor below the user's level, also while gliding there:
/// the overview keeps the view from before it.</param>
internal readonly record struct FrameRequest(
    ZoomMode Mode,
    ZoomMode AnchorMode,
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
    ViewPlacement Placement = default,
    bool ViewHeld = false)
{
    /// <summary>Not magnified and no colour filter: only pointer, crosshair or locator are drawn.</summary>
    public bool OverlayOnly => Zoom <= 1.01 && !Effects.Filter.ChangesColors && !WindowsFiltered;
}
