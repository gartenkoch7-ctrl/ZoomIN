using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Sharing;

/// <summary>A detected share: what is shared and where, in physical desktop pixels.</summary>
public readonly record struct SharedArea(ShareKind Kind, PhysicalRect Bounds);
