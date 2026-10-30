namespace ZoomIN.Core.Viewport;

/// <summary>A rectangle in physical (unscaled) desktop pixels.</summary>
public readonly record struct PhysicalRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool Contains(PhysicalPoint point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;
}
