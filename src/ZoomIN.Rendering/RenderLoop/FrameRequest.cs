using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;

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
/// <param name="Activity">Mouse, typing and hold key for the show conditions of the highlights (E1).</param>
/// <param name="Frozen">The monitor is frozen (G2): anchor, level and mode stay, no pointer or marks; null = live.</param>
/// <param name="FrozenStill">Frozen with a still image that is (to be) shown, read once per frame: <see cref="FrozenMonitor.StillUnavailable"/> may change during a freeze.</param>
/// <param name="Islands">Islands to draw on this monitor (M19 B1), or null; drawing them needs the desktop even at 1× (edit mode).</param>
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
    bool ViewHeld = false,
    MarkerActivitySample Activity = default,
    FrozenMonitor? Frozen = null,
    bool FrozenStill = false,
    IslandScene? Islands = null)
{
    /// <summary>
    /// Not magnified, no colour filter and no islands: only pointer, crosshair or locator are drawn. A
    /// frozen still image is drawn at 1× too, or the live desktop would show through.
    /// </summary>
    public bool OverlayOnly =>
        Zoom <= ZoomLimits.UnzoomedUpTo && !Effects.Filter.ChangesColors && !WindowsFiltered && !FrozenStill && Islands is null;
}
