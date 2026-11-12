using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.PointerFence;

/// <summary>
/// Where the pointer may go: the union of the monitors minus the forbidden rectangles (frozen
/// monitors, M19). Built once per change as disjoint rectangles ("pieces"); the per-move queries
/// (<see cref="IsAllowed"/>, <see cref="Constrain"/>) only loop over them and never allocate,
/// because they run inside the low-level mouse hook.
/// </summary>
/// <remarks>
/// Coordinates are physical desktop pixels on the pixel grid: a piece covers
/// [X, Right) × [Y, Bottom), so the last reachable pixel is Right − 1 (as with RECT and ClipCursor).
/// </remarks>
public sealed class FenceGeometry
{
    private readonly PhysicalRect[] _pieces;

    private FenceGeometry(PhysicalRect[] pieces, bool restricted)
    {
        _pieces = pieces;
        IsRestricted = restricted;
        SingleRect = Bounding(pieces);
    }

    /// <summary>True if at least one forbidden rectangle overlaps a monitor.</summary>
    public bool IsRestricted { get; }

    /// <summary>No allowed pixel left: a fence must then stay off, the pointer is never trapped.</summary>
    public bool IsEmpty => _pieces.Length == 0;

    /// <summary>
    /// The allowed area as one rectangle if it is exactly that (then one ClipCursor call is
    /// enough); null if it has another shape and needs the mouse hook.
    /// </summary>
    public PhysicalRect? SingleRect { get; }

    /// <summary>The disjoint rectangles that make up the allowed area.</summary>
    public IReadOnlyList<PhysicalRect> Pieces => _pieces;

    public static FenceGeometry Create(IReadOnlyList<PhysicalRect> monitors, IReadOnlyList<PhysicalRect> forbidden)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(forbidden);

        var pieces = new List<PhysicalRect>(monitors.Count);
        foreach (var monitor in monitors)
        {
            // Mirrored displays overlap (often with the same rectangle): only the part not yet
            // covered is added, so the pieces stay disjoint.
            var fresh = new List<PhysicalRect> { monitor };
            foreach (var piece in pieces)
            {
                fresh = Cut(fresh, piece, out _);
            }

            pieces.AddRange(fresh.Where(rect => !IsEmptyRect(rect)));
        }

        var restricted = false;
        foreach (var cut in forbidden)
        {
            pieces = Cut(pieces, cut, out var overlapped);
            restricted |= overlapped;
        }

        return new FenceGeometry([.. pieces], restricted);
    }

    public bool IsAllowed(PhysicalPoint point) => PieceAt(point) >= 0;

    /// <summary>The allowed pixel closest to <paramref name="point"/>; the point itself if allowed or nothing is.</summary>
    public PhysicalPoint Nearest(PhysicalPoint point)
    {
        var best = point;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < _pieces.Length; i++)
        {
            var candidate = Clamp(_pieces[i], point);
            var dx = candidate.X - point.X;
            var dy = candidate.Y - point.Y;
            var distance = (dx * dx) + (dy * dy);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Where a move from <paramref name="current"/> to <paramref name="requested"/> may end: the
    /// requested point if it is allowed, otherwise the closest point of the piece the pointer is
    /// in, so it stops at the edge instead of jumping over a forbidden area. A pointer that is
    /// already outside the allowed area (display change, moved by another program) moves freely:
    /// the fence never traps it.
    /// </summary>
    public PhysicalPoint Constrain(PhysicalPoint current, PhysicalPoint requested)
    {
        if (IsAllowed(requested))
        {
            return requested;
        }

        var from = PieceAt(current);
        return from < 0 ? requested : Clamp(_pieces[from], requested);
    }

    private int PieceAt(PhysicalPoint point)
    {
        for (var i = 0; i < _pieces.Length; i++)
        {
            if (_pieces[i].Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    private static PhysicalPoint Clamp(PhysicalRect piece, PhysicalPoint point) => new(
        Math.Clamp(point.X, piece.X, Math.Max(piece.X, piece.Right - 1)),
        Math.Clamp(point.Y, piece.Y, Math.Max(piece.Y, piece.Bottom - 1)));

    private static bool IsEmptyRect(PhysicalRect rect) => !(rect.Width > 0 && rect.Height > 0);

    private static bool Overlaps(PhysicalRect a, PhysicalRect b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    private static List<PhysicalRect> Cut(List<PhysicalRect> pieces, PhysicalRect cut, out bool overlapped)
    {
        overlapped = false;
        if (IsEmptyRect(cut))
        {
            return pieces;
        }

        var next = new List<PhysicalRect>(pieces.Count + 4);
        foreach (var piece in pieces)
        {
            if (Overlaps(piece, cut))
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

    // The pieces are disjoint, so they fill their bounding box exactly when their areas add up to it.
    private static PhysicalRect? Bounding(PhysicalRect[] pieces)
    {
        if (pieces.Length == 0)
        {
            return null;
        }

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
}
