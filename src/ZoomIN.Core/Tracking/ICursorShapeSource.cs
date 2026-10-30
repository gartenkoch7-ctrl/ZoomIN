namespace ZoomIN.Core.Tracking;

/// <summary>
/// Current mouse pointer shape. <see cref="TryGetCurrent"/> runs every frame on render threads
/// (allocation-free); <see cref="Load"/> only for handles not seen before, results are cached.
/// </summary>
public interface ICursorShapeSource
{
    /// <returns>False if no pointer is shown (e.g. hidden by an application while typing).</returns>
    bool TryGetCurrent(out nint handle);

    /// <returns>Null if the shape cannot be read.</returns>
    CursorShape? Load(nint handle);
}
