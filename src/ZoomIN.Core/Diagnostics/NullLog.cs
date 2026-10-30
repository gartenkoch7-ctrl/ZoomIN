namespace ZoomIN.Core.Diagnostics;

/// <summary>Discards all messages. Default for tests and for code running before logging is set up.</summary>
public sealed class NullLog : ILog
{
    public static NullLog Instance { get; } = new();

    private NullLog()
    {
    }

    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
