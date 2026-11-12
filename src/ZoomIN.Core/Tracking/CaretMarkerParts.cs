using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Screen rectangles of one caret marker (E2), computed per frame without allocation.
/// <list type="bullet">
/// <item>Bar: <see cref="First"/> filled.</item>
/// <item>Wedges: <see cref="First"/> above (tip down), <see cref="Second"/> below (tip up).</item>
/// <item>Frame: <see cref="First"/> hollow with <see cref="Line"/> wide edges.</item>
/// <item>Circle: ring inside the square <see cref="First"/>, <see cref="Line"/> wide.</item>
/// </list>
/// </summary>
public readonly record struct CaretMarkerParts(CaretMarkerShape Shape, PhysicalRect First, PhysicalRect Second, double Line);
