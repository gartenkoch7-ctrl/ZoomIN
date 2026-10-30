using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Capture;

/// <summary>A monitor that was enumerated a moment ago is no longer attached (unplugged, Win+P).</summary>
public sealed class DisplayGoneException : DisplayChangedException
{
    public DisplayGoneException()
        : base("A monitor is no longer attached.")
    {
    }

    public DisplayGoneException(string deviceName)
        : base($"Monitor {deviceName} is no longer attached.")
    {
    }

    public DisplayGoneException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
