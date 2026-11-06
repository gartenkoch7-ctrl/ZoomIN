using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
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
public sealed record RenderModels(ZoomModel Zoom, EffectsModel Effects, TrackingModel Tracking, WindowFilterModel Windows, DebugSnapshots Snapshots);
