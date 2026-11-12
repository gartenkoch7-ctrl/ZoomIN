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
/// switch, settings page), which the edit mode must not overwrite. Each calls <c>end</c> with a reason.
/// </summary>
internal sealed class IslandEditWatch : IDisposable
{
    private readonly IPointerFence _fence;
    private readonly ZoomModel _zoom;
    private readonly IslandModel _model;
    private readonly IMonitorProvider _monitors;
    private readonly Func<IslandSet?> _published;
    private readonly Action<string> _end;
    private readonly ILogger _log;

    /// <param name="published">The set the running edit mode last published, or null while it is off.</param>
    public IslandEditWatch(
        IPointerFence fence, ZoomModel zoom, IslandModel model, IMonitorProvider monitors, Func<IslandSet?> published, Action<string> end,
        ILogger log)
    {
        _fence = fence;
        _zoom = zoom;
        _model = model;
        _monitors = monitors;
        _published = published;
        _end = end;
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
        if (trigger is FenceTrigger.DesktopSwitch or FenceTrigger.SessionUnlock or FenceTrigger.DisplayChange)
        {
            End(trigger.ToString());
        }
    }

    // A zoom command of the user (zoom off, mode, step to 1×) cleared the edit mode's 1×.
    private void OnTemporaryChanged()
    {
        if (_published() is null)
        {
            return;
        }

        foreach (var monitor in _monitors.MonitorIds())
        {
            if (_zoom.Temporary.LevelFor(TemporaryZoomOwner.IslandEdit, monitor) is null)
            {
                End("cleared");
                return;
            }
        }
    }

    // Raised for the edit mode's own steps too; only a set it did not publish is a change from outside.
    private void OnModelChanged()
    {
        if (_published() is { } published && !ReferenceEquals(_model.Scene.Set, published))
        {
            End("changed");
        }
    }

    // Raised on other threads (fence watcher, zoom command on the UI thread): never let it fail there.
    private void End(string reason)
    {
        try
        {
            _end(reason);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.EditFailed(ex, "end");
        }
    }
}
