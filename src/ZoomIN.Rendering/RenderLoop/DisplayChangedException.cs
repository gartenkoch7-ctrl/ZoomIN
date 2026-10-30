namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Monitor layout, resolution, orientation or DPI changed: the pipeline must be rebuilt.
/// An expected event, not an error – the engine rebuilds immediately without waiting.
/// </summary>
public class DisplayChangedException : Exception
{
    public DisplayChangedException()
        : base("The display configuration changed.")
    {
    }

    public DisplayChangedException(string message)
        : base(message)
    {
    }

    public DisplayChangedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
