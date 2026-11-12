using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// What the render threads draw for the edit mode (M19 B1): blinking frames, the corner handles of
/// the selected island, the dashed rubber band. Immutable; a new view per input.
/// </summary>
/// <param name="Selected">Index of the selected island in the scene's set, or −1.</param>
/// <param name="RubberBand">The rectangle being dragged out, or null.</param>
/// <param name="StartedAt">When the edit mode began (<see cref="System.Diagnostics.Stopwatch"/> ticks); the blink counts from it.</param>
public sealed record IslandEditView(int Selected, PhysicalRect? RubberBand, long StartedAt)
{
    /// <summary>Half a blink period: the frames change colour this often.</summary>
    public const double BlinkSeconds = 0.5;
}
