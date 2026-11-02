namespace ZoomIN.Core.Tracking;

/// <summary>
/// Current mouse pointer shape. <see cref="TryGetCurrent"/> runs every frame on render threads
/// (allocation-free); <see cref="Load"/> only for handles not seen before, results are cached.
/// </summary>
public interface ICursorShapeSource
{
    /// <returns>False if no pointer is shown (e.g. hidden by an application while typing).</returns>
    bool TryGetCurrent(out nint handle);

    /// <summary>The standard arrow, e.g. for the preview on the pointer page.</summary>
    nint ArrowHandle();

    /// <returns>Null if the shape cannot be read.</returns>
    CursorShape? Load(nint handle);

    /// <summary>
    /// The standard Windows pointer for this handle, ignoring the Windows pointer size, colour and
    /// scheme, loaded with enough pixels to stay sharp up to pointer size <paramref name="scale"/>.
    /// Pointers without a standard counterpart (application pointers) fall back to
    /// <see cref="Load"/>, shrunk to standard size.
    /// </summary>
    CursorShape? LoadStandard(nint handle, double scale);
}
