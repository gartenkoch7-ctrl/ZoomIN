using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Overview;

/// <summary>
/// One overview (B2) on one monitor, from the shortcut until zooming in or Esc. The render thread
/// of that monitor records once which part of the desktop it showed before (<see cref="CaptureStart"/>),
/// so the frame starts as the previous view and Esc can return there.
/// </summary>
public sealed class OverviewSession
{
    // 0 = nothing recorded, 1 = the render thread is writing, 2 = published.
    private int _captured;
    private PhysicalRect _view;
    private PhysicalPoint _mouse;

    public OverviewSession(long id, string monitorId, ZoomMode mode, double levelBefore, long startedAt)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        Id = id;
        MonitorId = monitorId;
        Mode = mode;
        LevelBefore = levelBefore;
        StartedAt = startedAt;
    }

    /// <summary>Counts up per overview, so a late signal of an ended overview cannot touch the next one.</summary>
    public long Id { get; }

    public string MonitorId { get; }

    /// <summary>Mode at the start; a mode change ends the overview (it clears the temporary level).</summary>
    public ZoomMode Mode { get; }

    /// <summary>The user's level when the overview started (telemetry; the frame uses the live level).</summary>
    public double LevelBefore { get; }

    /// <summary><see cref="TimeProvider.GetTimestamp"/> of the key press.</summary>
    public long StartedAt { get; }

    /// <summary>
    /// Records the view shown before the overview and the mouse at that moment. Render thread,
    /// first frame of the overview; later calls do nothing. No allocation.
    /// </summary>
    public void CaptureStart(PhysicalRect view, PhysicalPoint mouse)
    {
        if (Interlocked.CompareExchange(ref _captured, 1, 0) != 0)
        {
            return;
        }

        _view = view;
        _mouse = mouse;
        Volatile.Write(ref _captured, 2); // publishes both values to other threads
    }

    /// <summary>The view and mouse before the overview, once the render thread recorded them.</summary>
    public bool TryGetStart(out PhysicalRect view, out PhysicalPoint mouse)
    {
        if (Volatile.Read(ref _captured) == 2)
        {
            view = _view;
            mouse = _mouse;
            return true;
        }

        view = default;
        mouse = default;
        return false;
    }
}
