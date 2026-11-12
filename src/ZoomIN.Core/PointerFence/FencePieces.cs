using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.PointerFence;

/// <summary>
/// Builds the allowed area of <see cref="FenceGeometry"/> as disjoint whole-pixel rectangles:
/// monitors minus forbidden rectangles. Runs once per change, never per move.
/// </summary>
internal static class FencePieces
{
    public static PhysicalRect[] Build(IReadOnlyList<PhysicalRect> monitors, IReadOnlyList<PhysicalRect> forbidden, out bool restricted)
    {
        var pieces = new List<PhysicalRect>(monitors.Count);
        foreach (var monitor in monitors)
        {
            // Mirrored displays overlap (often with the same rectangle): only the part not yet
            // covered is added, so the pieces stay disjoint.
            var fresh = new List<PhysicalRect> { RoundOut(monitor) };
            foreach (var piece in pieces)
            {
                fresh = Cut(fresh, piece, out _);
            }

            pieces.AddRange(fresh.Where(rect => !RectMath.IsEmpty(rect)));
        }

        restricted = false;
        foreach (var cut in forbidden)
        {
            pieces = Cut(pieces, cut, out var overlapped);
            restricted |= overlapped;
        }

        return [.. pieces];
    }

    /// <summary>The pieces as one rectangle if they fill exactly that; null otherwise or if there are none.</summary>
    public static PhysicalRect? Bounding(PhysicalRect[] pieces)
    {
        if (pieces.Length == 0)
        {
            return null;
        }

        // The pieces are disjoint, so they fill their bounding box exactly when their areas add up to it.
        double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue, area = 0;
        foreach (var piece in pieces)
        {
            left = Math.Min(left, piece.X);
            top = Math.Min(top, piece.Y);
            right = Math.Max(right, piece.Right);
            bottom = Math.Max(bottom, piece.Bottom);
            area += piece.Width * piece.Height;
        }

        var box = new PhysicalRect(left, top, right - left, bottom - top);
        return Math.Abs((box.Width * box.Height) - area) < 0.5 ? box : null;
    }



    // Whole pixels, rounded outwards: a clamped point is then a pixel the pointer can really
    // stand on, never one that SetCursorPos would round into the forbidden area.
    private static PhysicalRect RoundOut(PhysicalRect rect)
    {
        var left = Math.Floor(rect.X);
        var top = Math.Floor(rect.Y);
        return new PhysicalRect(left, top, Math.Ceiling(rect.Right) - left, Math.Ceiling(rect.Bottom) - top);
    }

    private static List<PhysicalRect> Cut(List<PhysicalRect> pieces, PhysicalRect cut, out bool overlapped)
    {
        overlapped = false;
        if (RectMath.IsEmpty(cut))
        {
            return pieces;
        }

        cut = RoundOut(cut);
        var next = new List<PhysicalRect>(pieces.Count + 4);
        foreach (var piece in pieces)
        {
            if (RectMath.Intersects(piece, cut))
            {
                overlapped = true;
                Subtract(piece, cut, next);
            }
            else
            {
                next.Add(piece);
            }
        }

        return next;
    }

    // Up to four disjoint pieces of 'piece' outside 'cut': full-width bands above and below,
    // then the parts left and right within the height of the cut.
    private static void Subtract(PhysicalRect piece, PhysicalRect cut, List<PhysicalRect> into)
    {
        var top = Math.Max(piece.Y, cut.Y);
        var bottom = Math.Min(piece.Bottom, cut.Bottom);
        var left = Math.Max(piece.X, cut.X);
        var right = Math.Min(piece.Right, cut.Right);
        if (top > piece.Y)
        {
            into.Add(new PhysicalRect(piece.X, piece.Y, piece.Width, top - piece.Y));
        }

        if (bottom < piece.Bottom)
        {
            into.Add(new PhysicalRect(piece.X, bottom, piece.Width, piece.Bottom - bottom));
        }

        if (left > piece.X)
        {
            into.Add(new PhysicalRect(piece.X, top, left - piece.X, bottom - top));
        }

        if (right < piece.Right)
        {
            into.Add(new PhysicalRect(right, top, piece.Right - right, bottom - top));
        }
    }
}
