namespace ZoomIN.Core.Islands;

/// <summary>
/// Everything the render threads need about islands, published as one immutable object (M19 B1):
/// the islands, whether they are shown (Ctrl+Alt+I) and the edit mode. A change is a new scene,
/// so the frame change tracker compares references.
/// </summary>
/// <param name="Edit">The edit mode, or null while it is off.</param>
public sealed record IslandScene(IslandSet Set, bool Visible, IslandEditView? Edit)
{
    public bool Editing => Edit is not null;

    /// <summary>
    /// Whether a monitor draws islands this frame: always while editing (the rubber band may start
    /// anywhere), otherwise when they are shown, the monitor is zoomed in fullscreen and an island
    /// lies on it (lens and docked: islands are a fullscreen feature, plan decision). 0 B.
    /// </summary>
    public bool ShowsOn(string monitorId, bool fullscreenZoomed) =>
        Editing || (Visible && fullscreenZoomed && Set.HasIslandOn(monitorId));
}
