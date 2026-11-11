using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Request to put the mouse view at a desktop point, e.g. "zoom in where the frame is" (M17 B2).
/// <see cref="AnchorController"/> anchors the view so <paramref name="Center"/> is in the middle at
/// <paramref name="Zoom"/> (clamped at the monitor edge) and the mouse takes the view from there.
/// Only <see cref="MouseFollowMode.Margin"/> honours it: the anchor holds while the zoom glides to
/// <paramref name="Zoom"/> (the pointer may be out of view meanwhile), then the margin rules.
/// Center and Fixpoint follow the mouse in the same frame, so a placement has no effect there.
/// </summary>
/// <param name="Center">Desktop point to show in the middle of the view.</param>
/// <param name="Zoom">Zoom level the view is placed for – the level the zoom glides to, not the current one.</param>
/// <param name="At">Stopwatch timestamp of the request; a newer one is news, 0 = none.</param>
public readonly record struct ViewPlacement(PhysicalPoint Center, double Zoom, long At);
