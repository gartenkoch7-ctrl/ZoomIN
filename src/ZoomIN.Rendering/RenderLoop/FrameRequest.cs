using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>Everything the user wants for one frame of one monitor, read from the models.</summary>
internal readonly record struct FrameRequest(
    ZoomMode Mode,
    double Zoom,
    LensSize Lens,
    DockLayout Dock,
    DisplayEffects Effects,
    long LocateRequestedAt,
    TrackingSnapshot Tracking,
    TrackingOptions TrackingOptions);
