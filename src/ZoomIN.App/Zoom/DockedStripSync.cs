using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Zoom;

/// <summary>
/// Keeps the AppBar reservation in line with the model: reserved while docked mode is on,
/// released otherwise. Called after every model change, from any thread.
/// </summary>
public sealed class DockedStripSync
{
    private readonly ZoomModel _model;
    private readonly IMonitorProvider _monitors;
    private readonly IScreenReservation _reservation;
    private readonly Lock _gate = new();
    private (PhysicalRect Area, DockEdge Edge)? _reserved;

    public DockedStripSync(ZoomModel model, IMonitorProvider monitors, IScreenReservation reservation)
    {
        _model = model;
        _monitors = monitors;
        _reservation = reservation;
    }

    public void Update()
    {
        lock (_gate)
        {
            var wanted = Wanted();
            if (wanted == _reserved)
            {
                return;
            }

            if (wanted is { } strip)
            {
                _reservation.Reserve(strip.Area, strip.Edge);
            }
            else
            {
                _reservation.Release();
            }

            _reserved = wanted;
        }
    }

    private (PhysicalRect Area, DockEdge Edge)? Wanted()
    {
        if (_model.Mode != ZoomMode.Docked || !_model.IsOn)
        {
            return null;
        }

        var dock = _model.Dock;
        return _monitors.BoundsOf(_model.DockedMonitor) is { } bounds
            ? (ModeGeometry.DockRect(bounds, dock), dock.Edge)
            : null;
    }
}
