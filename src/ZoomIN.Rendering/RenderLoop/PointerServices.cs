using ZoomIN.Core.Tracking;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>Everything a render thread needs about the mouse pointer.</summary>
/// <param name="Position">Where the pointer is.</param>
/// <param name="Shapes">What it looks like.</param>
/// <param name="SystemCursor">Hides the real pointer while the magnified one is drawn.</param>
public sealed record PointerServices(ICursorSource Position, ICursorShapeSource Shapes, ISystemCursor SystemCursor);
