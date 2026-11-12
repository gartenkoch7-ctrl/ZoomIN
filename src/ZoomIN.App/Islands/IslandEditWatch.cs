using Microsoft.Extensions.Logging;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Islands;

/// <summary>
/// The safety ends of the island edit mode (M19 B1) besides Esc and the idle time: a desktop switch,
/// an unlock or a display change (the fence reports them, it runs because of the drag filter), a
/// zoom command that cleared the edit mode's 1×, and a change of the islands from outside (profile
/// switch, settings page), which the edit mode must not overwrite.
/// </summary>
internal sealed class IslandEditWatch : IDisposable
{
    private readonly IPointerFence _fence;
    private readonly ZoomModel _zoom;
    private readonly IslandModel _model;
    private readonly IMonitorProvider _monitors;
    private readonly Func<Action<string>?> _endOfRun;
    private readonly Action _checkOutside;
    private readonly ILogger _log;

    /// <param name="endOfRun">Ends the run that is current when it is called (never a later one), or null while none runs.</param>
    /// <param name="checkOutside">Ends it if the islands changed from outside (the controller compares under its lock).</param>
    public IslandEditWatch(
        IPointerFence fence, ZoomModel zoom, IslandModel model, IMonitorProvider monitors, Func<Action<string>?> endOfRun,
        Action checkOutside, ILogger log)
    {
        _fence = fence;
        _zoom = zoom;
        _model = model;
        _monitors = monitors;
        _endOfRun = endOfRun;
        _checkOutside = checkOutside;
        _log = log;
        _fence.Recomputed += OnFenceRecomputed;
        _zoom.Temporary.Changed += OnTemporaryChanged;
        _model.Changed += OnModelChanged;
    }

    public void Dispose()
    {
        _fence.Recomputed -= OnFenceRecomputed;
        _zoom.Temporary.Changed -= OnTemporaryChanged;
        _model.Changed -= OnModelChanged;
    }

    // Win+L, UAC, Ctrl+Alt+Del or new monitors: never keep the clicks across them.
    private void OnFenceRecomputed(object? sender, FenceTrigger trigger)
    {
        // On the thread pool, not on the fence's watcher thread: ending gives the drag filter back to the
        // fence, which may retire (join) that very thread, and a start in progress may wait for the fence.
        if (trigger is FenceTrigger.DesktopSwitch or FenceTrigger.SessionUnlock or FenceTrigger.DisplayChange)
        {
            var reason = trigger.ToString();
            if (_endOfRun() is { } end)
            {
                _ = Task.Run(() => Safely(() => end(reason)));
            }
        }
    }

    // A zoom command of the user (zoom off, mode, step to 1×) cleared the edit mode's 1×.
    private void OnTemporaryChanged()
    {
        if (_endOfRun() is not { } end)
        {
            return;
        }

        foreach (var monitor in _monitors.MonitorIds())
        {
            if (_zoom.Temporary.LevelFor(TemporaryZoomOwner.IslandEdit, monitor) is null)
            {
                Safely(() => end("cleared"));
                return;
            }
        }
    }

    // Also raised for the edit mode's own steps; the controller tells them apart under its lock.
    private void OnModelChanged()
    {
        if (_endOfRun() is not null)
        {
            Safely(_checkOutside);
        }
    }

    // Raised on other threads (fence watcher, zoom command on the UI thread): never let it fail there.
    private void Safely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.EditFailed(ex, "end");
        }
    }
}
