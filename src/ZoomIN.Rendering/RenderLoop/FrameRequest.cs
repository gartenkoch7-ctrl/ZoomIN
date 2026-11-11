using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>Everything the user wants for one frame of one monitor, read from the models.</summary>
/// <param name="Windows">Window regions of the filter per window.</param>
/// <param name="WindowsFiltered">Some window on this monitor looks different from the rest (ADR-0020).</param>
/// <param name="Activity">Mouse, typing and hold key for the show conditions of the highlights (E1).</param>
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
    MarkerActivitySample Activity = default)
{
    /// <summary>Not magnified and no colour filter: only pointer, crosshair or locator are drawn.</summary>
    public bool OverlayOnly => Zoom <= 1.01 && !Effects.Filter.ChangesColors && !WindowsFiltered;
}
