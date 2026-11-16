using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Pointer;

/// <summary>
/// Turns global input activity into locator requests (shaking, moving after a rest, Ctrl alone)
/// and into hiding the pointer while typing; feeds the show conditions of the highlights (E1).
/// Runs on the hook thread: no allocations per move.
/// </summary>
internal sealed class PointerActivity : IInputActivityHandler
{
    private readonly EffectsModel _effects;
    private readonly MarkerActivity _markers;
    private readonly ShakeDetector _shake = new();
    private readonly IdleDetector _idle = new();

    public PointerActivity(EffectsModel effects, MarkerActivity markers)
    {
        _effects = effects;
        _markers = markers;
    }

    public void MouseMoved(long timestamp, int x, int y)
    {
        _effects.MarkMoved();
        _markers.Moved();
        var triggers = _effects.Current.Cursor.LocatorTriggers;
        var shaken = _shake.Add(timestamp, x);
        var rested = _idle.Add(timestamp);
        if ((shaken && triggers.HasFlag(LocatorTriggers.Shake)) || (rested && triggers.HasFlag(LocatorTriggers.AfterIdle)))
        {
            _effects.Locate();
        }
    }

    public void Typed()
    {
        _effects.MarkTyped();
        _markers.Typed();
    }

    public void CtrlTapped()
    {
        // Ctrl as hold key for the highlights (E1): releasing it after a hold is not "Ctrl alone".
        var ctrlHeld = _markers.Key is MarkerHoldKey.LeftCtrl or MarkerHoldKey.RightCtrl && _markers.ConsumeHoldEnd();
        if (_effects.Current.Cursor.LocatorTriggers.HasFlag(LocatorTriggers.CtrlKey) && !ctrlHeld)
        {
            _effects.Locate();
        }
    }
}
