using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Modes;

/// <summary>
/// Per-frame geometry of the three zoom modes. Pure math, called by the render thread every
/// frame: no allocations (AGENTS.md 2.3).
/// </summary>
public static class ModeGeometry
{
    public static ModeFrame Compute(
        ZoomMode mode, PhysicalRect monitor, PhysicalPoint cursor, double zoom, LensSize lens, DockLayout dock) => mode switch
        {
            ZoomMode.Lens => Lens(monitor, cursor, zoom, lens),
            ZoomMode.Docked => Docked(monitor, cursor, zoom, dock),
            _ => new ModeFrame(monitor, ViewportCalculator.SourceRect(monitor, cursor, zoom), false),
        };

    /// <summary>
    /// Lens centred on the cursor, kept inside the monitor. Magnified around the cursor as fixpoint
    /// (source = p + (target − p) / z), so the pixel under the pointer stays in place and clicks hit.
    /// </summary>
    public static ModeFrame Lens(PhysicalRect monitor, PhysicalPoint cursor, double zoom, LensSize lens)
    {
        var z = Math.Max(zoom, ViewportCalculator.MinZoom);
        var width = monitor.Width * lens.WidthFraction;
        var height = monitor.Height * lens.HeightFraction;
        var p = Clamp(cursor, monitor);

        var target = new PhysicalRect(
            Math.Clamp(p.X - (width / 2), monitor.X, monitor.Right - width),
            Math.Clamp(p.Y - (height / 2), monitor.Y, monitor.Bottom - height),
            width,
            height);
        var source = new PhysicalRect(
            p.X + ((target.X - p.X) / z),
            p.Y + ((target.Y - p.Y) / z),
            width / z,
            height / z);
        return new ModeFrame(target, source, true);
    }

    /// <summary>
    /// Docked strip shows the area around the cursor, taken only from the rest of the monitor so
    /// the strip never magnifies itself. The view is display-only; clicks in the strip do nothing.
    /// </summary>
    public static ModeFrame Docked(PhysicalRect monitor, PhysicalPoint cursor, double zoom, DockLayout dock)
    {
        var z = Math.Max(zoom, ViewportCalculator.MinZoom);
        var target = DockRect(monitor, dock);
        var rest = RestRect(monitor, dock);

        var width = Math.Min(target.Width / z, rest.Width);
        var height = Math.Min(target.Height / z, rest.Height);
        var p = Clamp(cursor, rest);
        var source = new PhysicalRect(
            Math.Clamp(p.X - (width / 2), rest.X, rest.Right - width),
            Math.Clamp(p.Y - (height / 2), rest.Y, rest.Bottom - height),
            width,
            height);
        return new ModeFrame(target, source, true);
    }

    /// <summary>The reserved strip (also used for the AppBar reservation).</summary>
    public static PhysicalRect DockRect(PhysicalRect monitor, DockLayout dock)
    {
        var f = dock.Fraction;
        return dock.Edge switch
        {
            DockEdge.Bottom => new PhysicalRect(monitor.X, monitor.Bottom - (monitor.Height * f), monitor.Width, monitor.Height * f),
            DockEdge.Left => new PhysicalRect(monitor.X, monitor.Y, monitor.Width * f, monitor.Height),
            DockEdge.Right => new PhysicalRect(monitor.Right - (monitor.Width * f), monitor.Y, monitor.Width * f, monitor.Height),
            _ => new PhysicalRect(monitor.X, monitor.Y, monitor.Width, monitor.Height * f),
        };
    }

    /// <summary>The part of the monitor not covered by the docked strip.</summary>
    public static PhysicalRect RestRect(PhysicalRect monitor, DockLayout dock)
    {
        var strip = DockRect(monitor, dock);
        return dock.Edge switch
        {
            DockEdge.Bottom => monitor with { Height = monitor.Height - strip.Height },
            DockEdge.Left => monitor with { X = strip.Right, Width = monitor.Width - strip.Width },
            DockEdge.Right => monitor with { Width = monitor.Width - strip.Width },
            _ => monitor with { Y = strip.Bottom, Height = monitor.Height - strip.Height },
        };
    }

    private static PhysicalPoint Clamp(PhysicalPoint p, PhysicalRect r) =>
        new(Math.Clamp(p.X, r.X, r.Right), Math.Clamp(p.Y, r.Y, r.Bottom));
}
