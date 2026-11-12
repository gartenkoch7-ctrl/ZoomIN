using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Islands;

/// <summary>
/// The island edit mode as a pure state machine (M19 B1): drag a rubber band on empty space for a
/// new island, drag an island to move it, drag one of its corners to show more or less of its
/// source, wheel over it for its level, Del removes the selected one, Esc undoes a drag or ends.
/// All points are physical desktop pixels; the edit mode shows the monitors at 1×, so they are also
/// where the user sees the islands. Not thread-safe: the controller feeds it one input at a time.
/// </summary>
public sealed class IslandEditor
{
    /// <summary>How far outside or inside a corner a press still grabs it, in pixels.</summary>
    public const double HandleReach = 12;

    private readonly IMonitorProvider _monitors;
    private readonly Func<string, double> _startLevel;
    private Drag _drag;
    private IslandSet _before;
    private PhysicalPoint _last;

    /// <param name="startLevel">Level of a new island on a monitor (the monitor's current level).</param>
    public IslandEditor(IslandSet start, IMonitorProvider monitors, Func<string, double> startLevel)
    {
        ArgumentNullException.ThrowIfNull(start);
        Set = start;
        _before = start;
        _monitors = monitors;
        _startLevel = startLevel;
    }

    /// <summary>The islands as they look now, including a drag in progress.</summary>
    public IslandSet Set { get; private set; }

    /// <summary>Index of the selected island in <see cref="Set"/>, or −1.</summary>
    public int Selected { get; private set; } = -1;

    /// <summary>The rubber band while a new island is dragged out, or null.</summary>
    public PhysicalRect? RubberBand { get; private set; }

    /// <summary>Bounds of the monitor a drag is confined to, or null while nothing is dragged.</summary>
    public PhysicalRect? DragBounds => _drag.Kind == DragKind.None ? null : _drag.Monitor;

    public IslandEditOutcome Press(PhysicalPoint point)
    {
        _last = point;
        Escape(cancelOnly: true);
        var index = Set.HitTest(point, HandleReach);
        if (index >= 0 && _monitors.BoundsOf(Set[index].MonitorId) is { } bounds)
        {
            var target = Set[index].Target;
            var corner = CornerAt(target, point);

            // Near an edge but not at a corner: a press beside the island, not on it.
            if (corner is not null || target.Contains(point))
            {
                Selected = index;
                _before = Set;
                _drag = new Drag(corner is null ? DragKind.Move : DragKind.Resize, bounds, point, Set[index].Id, corner ?? default);
                return IslandEditOutcome.Selected;
            }
        }

        var monitorId = _monitors.MonitorAt(point);
        if (_monitors.BoundsOf(monitorId) is not { } monitor || !monitor.Contains(point))
        {
            return IslandEditOutcome.None;
        }

        Selected = -1;
        _before = Set;
        _drag = new Drag(DragKind.Create, monitor, point, 0, default) { MonitorId = monitorId };
        RubberBand = new PhysicalRect(point.X, point.Y, 0, 0);
        return IslandEditOutcome.Dragged;
    }

    public IslandEditOutcome Move(PhysicalPoint point)
    {
        _last = point;
        switch (_drag.Kind)
        {
            case DragKind.Create:
                RubberBand = Span(_drag.Start, Clamp(point, _drag.Monitor));
                return IslandEditOutcome.Dragged;
            case DragKind.Move:
            case DragKind.Resize:
                var index = _before.IndexOf(_drag.IslandId);
                if (index < 0)
                {
                    return IslandEditOutcome.None;
                }

                var island = _before[index];
                var moved = _drag.Kind == DragKind.Move
                    ? island with { TargetOrigin = new PhysicalPoint(
                        island.TargetOrigin.X + point.X - _drag.Start.X, island.TargetOrigin.Y + point.Y - _drag.Start.Y) }
                    : IslandResize.ToCorner(island, _drag.Corner, point);
                Set = _before.Replace(index, IslandRules.Fit(moved, _drag.Monitor));
                return IslandEditOutcome.Dragged;
            default:
                return IslandEditOutcome.None;
        }
    }

    public IslandEditOutcome Release(PhysicalPoint point)
    {
        if (_drag.Kind == DragKind.None)
        {
            return IslandEditOutcome.None;
        }

        Move(point);
        var drag = _drag;
        _drag = default;
        if (drag.Kind != DragKind.Create)
        {
            return Set.Items.SequenceEqual(_before.Items) ? IslandEditOutcome.None : IslandEditOutcome.Changed;
        }

        var band = RubberBand ?? default;
        RubberBand = null;
        if (!IslandRules.IsLargeEnough(band))
        {
            return IslandEditOutcome.None;
        }

        var added = Set.Add(IslandRules.Create(0, drag.MonitorId!, band, drag.Monitor, _startLevel(drag.MonitorId!)));
        if (added is null)
        {
            return IslandEditOutcome.Full;
        }

        Set = added;
        Selected = added.Count - 1;
        return IslandEditOutcome.Created;
    }

    /// <summary>Wheel over an island (or with one selected): its level in zoom steps.</summary>
    public IslandEditOutcome Wheel(double notches, PhysicalPoint point)
    {
        _last = point;
        if (_drag.Kind != DragKind.None)
        {
            return IslandEditOutcome.None;
        }

        var index = Set.HitTest(point);
        index = index >= 0 ? index : Selected;
        if (index < 0 || _monitors.BoundsOf(Set[index].MonitorId) is not { } monitor)
        {
            return IslandEditOutcome.None;
        }

        var island = Set[index];
        Set = Set.Replace(index, IslandRules.WithLevel(island, ZoomLimits.Step(island.Level, notches), monitor));
        Selected = index;
        return IslandEditOutcome.LevelChanged;
    }

    /// <summary>Del: removes the selected island, or the one under the pointer.</summary>
    public IslandEditOutcome Delete()
    {
        if (_drag.Kind != DragKind.None)
        {
            return IslandEditOutcome.None;
        }

        var index = Selected >= 0 && Selected < Set.Count ? Selected : Set.HitTest(_last);
        if (index < 0)
        {
            return IslandEditOutcome.None;
        }

        Set = Set.RemoveAt(index);
        Selected = -1;
        return IslandEditOutcome.Deleted;
    }

    /// <summary>Esc: undoes a drag in progress, otherwise ends the edit mode.</summary>
    public IslandEditOutcome Escape() => Escape(cancelOnly: false);

    private IslandEditOutcome Escape(bool cancelOnly)
    {
        if (_drag.Kind == DragKind.None)
        {
            return cancelOnly ? IslandEditOutcome.None : IslandEditOutcome.End;
        }

        Set = _before;
        RubberBand = null;
        _drag = default;
        return IslandEditOutcome.Cancelled;
    }

    // A corner of the picture within reach of the point, or null.
    private static IslandCorner? CornerAt(PhysicalRect target, PhysicalPoint point)
    {
        var left = Math.Abs(point.X - target.X) <= HandleReach;
        var right = Math.Abs(point.X - target.Right) <= HandleReach;
        var top = Math.Abs(point.Y - target.Y) <= HandleReach;
        var bottom = Math.Abs(point.Y - target.Bottom) <= HandleReach;
        return (left, right, top, bottom) switch
        {
            (true, _, true, _) => IslandCorner.TopLeft,
            (_, true, true, _) => IslandCorner.TopRight,
            (true, _, _, true) => IslandCorner.BottomLeft,
            (_, true, _, true) => IslandCorner.BottomRight,
            _ => null,
        };
    }

    private static PhysicalPoint Clamp(PhysicalPoint point, PhysicalRect monitor) =>
        new(Math.Clamp(point.X, monitor.X, monitor.Right), Math.Clamp(point.Y, monitor.Y, monitor.Bottom));

    private static PhysicalRect Span(PhysicalPoint a, PhysicalPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private enum DragKind
    {
        None,
        Create,
        Move,
        Resize,
    }

    private readonly record struct Drag(DragKind Kind, PhysicalRect Monitor, PhysicalPoint Start, int IslandId, IslandCorner Corner)
    {
        public string? MonitorId { get; init; }
    }
}
