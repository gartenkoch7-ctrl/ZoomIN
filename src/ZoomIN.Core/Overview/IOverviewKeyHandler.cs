namespace ZoomIN.Core.Overview;

/// <summary>Receives the key signals of <see cref="IOverviewKeys"/> on the hook thread; must return quickly.</summary>
public interface IOverviewKeyHandler
{
    /// <summary>The watched shortcut was released (once per watch).</summary>
    /// <param name="late">Found by looking at the key state, not seen by the hook: the moment of the
    /// release is unknown (it came before or right at the start of the watch, so it was short).</param>
    void ShortcutReleased(long watchId, bool late);

    /// <summary>Esc went down while watching; it was swallowed.</summary>
    void EscapePressed(long watchId);
}
