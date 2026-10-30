using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>Everything <see cref="AnchorController"/> needs for one frame of one monitor.</summary>
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
    TrackingOptions Options);
