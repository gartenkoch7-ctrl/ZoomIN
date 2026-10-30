namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// Minimal logging abstraction. Not for use inside the render loop (allocates strings);
/// see docs/kb/performance.md.
/// </summary>
public interface ILog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
