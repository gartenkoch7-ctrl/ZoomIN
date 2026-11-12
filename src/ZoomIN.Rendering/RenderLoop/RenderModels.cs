using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>The models the render threads read every frame (all lock-free for readers).</summary>
/// <param name="Zoom">Mode, levels, lens and dock.</param>
/// <param name="Effects">Colour filter, smoothing, pointer style, locator.</param>
/// <param name="Tracking">Keyboard focus and text caret.</param>
/// <param name="Windows">Filter per window: regions from the window tracker (ADR-0020).</param>
/// <param name="Snapshots">Debug snapshots requested on the diagnostics page.</param>
/// <param name="SmartInvert">Fall-backs of the smart inversion, for the diagnostics page.</param>
/// <param name="Overview">The overview (B2): frame and the view before it.</param>
/// <param name="ViewRequests">"Centre the view on P" requests (M17), handed to the anchor as its placement.</param>
public sealed record RenderModels(
    ZoomModel Zoom, EffectsModel Effects, TrackingModel Tracking, WindowFilterModel Windows, DebugSnapshots Snapshots, SmartInvertHealth SmartInvert,
    OverviewModel Overview, ViewCenterRequests ViewRequests)
{
    /// <summary>Mouse, typing and hold key for the show conditions of the highlights (E1).</summary>
    public MarkerActivity Markers { get; init; } = new();

    /// <summary>Islands above the zoom and their edit mode (M19 B1).</summary>
    public IslandModel Islands { get; init; } = new(IslandSet.Empty, visible: true);
}
