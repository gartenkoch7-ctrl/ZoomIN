using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>Everything <see cref="AnchorController"/> needs for one frame of one monitor.</summary>
/// <param name="Placement">Latest request to place the view (M17 B2); default = none.</param>
public readonly record struct AnchorInput(
    long Now,
    PhysicalPoint Mouse,
    bool MouseInside,
    PhysicalRect Monitor,
    ZoomMode Mode,
    double Zoom,
    LensSize Lens,
    DockLayout Dock,
    TrackingSnapshot Tracking,
    TrackingOptions Options,
    ViewPlacement Placement = default);
