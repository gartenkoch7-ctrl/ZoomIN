namespace ZoomIN.Core.Shell;

/// <summary>
/// Short spoken status for screen readers (UI Automation notification), e.g. "Overview on",
/// also while no ZoomIN window is open. Does nothing if no screen reader listens.
/// </summary>
public interface IAnnouncer
{
    /// <param name="message">Localised text without user content.</param>
    void Announce(string message);
}
