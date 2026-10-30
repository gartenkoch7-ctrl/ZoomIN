namespace ZoomIN.Core.Tracking;

/// <summary>
/// Shows or hides the real Windows pointer while ZoomIN draws a magnified one (ADR-0011).
/// Thread-safe; repeated calls with the same value are free.
/// </summary>
public interface ISystemCursor : IDisposable
{
    void SetVisible(bool visible);
}
