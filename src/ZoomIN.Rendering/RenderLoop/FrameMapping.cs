using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>Maps desktop coordinates into the magnified image of a frame.</summary>
internal static class FrameMapping
{
    /// <summary>Where a desktop point appears on screen. Fullscreen/lens around the mouse: the point itself.</summary>
    public static PhysicalPoint Map(in ModeFrame frame, PhysicalPoint p) => new(
        frame.Target.X + ((p.X - frame.Source.X) * Scale(frame)),
        frame.Target.Y + ((p.Y - frame.Source.Y) * Scale(frame)));

    public static PhysicalRect Map(in ModeFrame frame, PhysicalRect r)
    {
        var topLeft = Map(frame, new PhysicalPoint(r.X, r.Y));
        return new PhysicalRect(topLeft.X, topLeft.Y, r.Width * Scale(frame), r.Height * Scale(frame));
    }

    /// <summary>Actual magnification of the frame (docked views can be clamped below the zoom).</summary>
    public static double Scale(in ModeFrame frame) => frame.Target.Width / frame.Source.Width;

    public static bool Intersects(PhysicalRect a, PhysicalRect b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
}
