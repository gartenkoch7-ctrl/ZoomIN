namespace ZoomIN.Core.Zoom;

/// <summary>
/// Feature that holds a temporary zoom level (M17). Each owner keeps its own level per monitor, so
/// the overview and a window drag at the same time do not release each other's level.
/// </summary>
public enum TemporaryZoomOwner
{
    /// <summary>B2: overview at 1× on a key press.</summary>
    Overview,

    /// <summary>G1: lift or lower the zoom while a window is moved or resized.</summary>
    WindowDrag,

    /// <summary>M19 B1: every monitor at 1× while islands are edited, so the pointer is where the user sees the islands.</summary>
    IslandEdit,
}
