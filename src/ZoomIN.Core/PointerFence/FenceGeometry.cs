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
        SingleRect = FencePieces.Bounding(pieces);
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
        var pieces = FencePieces.Build(monitors, forbidden, out var restricted);
        return new FenceGeometry(pieces, restricted);
    }

    public bool IsAllowed(PhysicalPoint point) => PieceAt(point) >= 0;

    /// <summary>
    /// The allowed pixel closest to <paramref name="point"/>; the point itself if allowed or nothing
    /// is. For choosing a target (e.g. where a shortcut puts the pointer), not for moves: it may pick
    /// a piece beyond a forbidden area. Moves go through <see cref="Constrain"/>.
    /// </summary>
    public PhysicalPoint Nearest(PhysicalPoint point)
    {
        var best = point;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < _pieces.Length; i++)
        {
            var candidate = Clamp(_pieces[i], point);
            var distance = Distance(candidate, point);
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
        if (from < 0)
        {
            return requested;
        }

        // The current piece or one touching it (pieces of one area share edges): the pointer
        // slides along a forbidden edge instead of sticking at an inner seam between pieces.
        var home = _pieces[from];
        var best = Clamp(home, requested);
        var bestDistance = Distance(best, requested);
        for (var i = 0; i < _pieces.Length; i++)
        {
            if (i != from && Touches(home, _pieces[i]))
            {
                var candidate = Clamp(_pieces[i], requested);
                var distance = Distance(candidate, requested);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private static double Distance(PhysicalPoint a, PhysicalPoint b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y));

    // A shared edge of positive length; a common corner alone does not count, or the pointer could
    // slip diagonally onto a third monitor next to a forbidden one.
    private static bool Touches(PhysicalRect a, PhysicalRect b) =>
        (a.X < b.Right && b.X < a.Right && (a.Y == b.Bottom || b.Y == a.Bottom))
        || (a.Y < b.Bottom && b.Y < a.Bottom && (a.X == b.Right || b.X == a.Right));

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
}
