using ZoomIN.Core.Islands;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Islands;

/// <summary>
/// What the island edit mode takes over while it runs (M19 B1) and gives back at its end: every
/// monitor at 1× (temporary level <see cref="TemporaryZoomOwner.IslandEdit"/>), the clicks, wheel,
/// Esc and Del (<see cref="IIslandEditInput"/>) and the drag filter of the pointer fence.
/// </summary>
internal sealed class IslandEditTakeover(ZoomModel zoom, IMonitorProvider monitors, IIslandEditInput input, IPointerFence fence)
{
    /// <summary>Keeps a drag on its monitor; confined by the edit steps.</summary>
    public IslandDragFence DragFence { get; } = new();

    /// <summary>The monitors set to 1× by the last <see cref="ShowOneTimes"/>.</summary>
    public IReadOnlyList<string> Monitors { get; private set; } = [];

    /// <summary>
    /// 1× on every monitor. First, before the edit mode counts as running: the levels' Changed must
    /// not find a running edit mode with a monitor still unset.
    /// </summary>
    public void ShowOneTimes()
    {
        Monitors = monitors.MonitorIds();
        foreach (var monitor in Monitors)
        {
            zoom.Temporary.Set(TemporaryZoomOwner.IslandEdit, monitor, ZoomLimits.Min);
        }
    }

    /// <summary>
    /// Why the edit mode cannot start, or null. Docked has no temporary 1× (the strip reserves screen
    /// space), so the pointer would not be where the islands are; without the hooks the clicks would
    /// reach the applications under the islands.
    /// </summary>
    public string? Refused() => zoom.Mode == ZoomMode.Docked ? "docked" : !input.IsAvailable ? "no_input" : null;

    /// <summary>The input and the drag filter, once the edit mode runs.</summary>
    public void TakeInput(IIslandEditInputHandler handler)
    {
        fence.AddFilter(DragFence);
        input.Start(handler);
    }

    /// <summary>Gives everything back; the clicks first, whatever fails afterwards.</summary>
    public void GiveBack()
    {
        try
        {
            input.Stop();
            DragFence.Confine(null);
        }
        finally
        {
            try
            {
                fence.RemoveFilter(DragFence);
            }
            finally
            {
                zoom.Temporary.ReleaseAll(TemporaryZoomOwner.IslandEdit);
            }
        }
    }
}
