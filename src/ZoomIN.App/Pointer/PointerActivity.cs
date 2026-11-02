using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;

namespace ZoomIN.App.Pointer;

/// <summary>
/// Turns global input activity into locator requests (shaking, moving after a rest, Ctrl alone)
/// and into hiding the pointer while typing. Runs on the hook thread: no allocations per move.
/// </summary>
internal sealed class PointerActivity : IInputActivityHandler
{
    private readonly EffectsModel _effects;
    private readonly ShakeDetector _shake = new();
    private readonly IdleDetector _idle = new();

    public PointerActivity(EffectsModel effects)
    {
        _effects = effects;
    }

    public void MouseMoved(long timestamp, int x, int y)
    {
        _effects.MarkMoved();
        var triggers = _effects.Current.Cursor.LocatorTriggers;
        var shaken = _shake.Add(timestamp, x);
        var rested = _idle.Add(timestamp);
        if ((shaken && triggers.HasFlag(LocatorTriggers.Shake)) || (rested && triggers.HasFlag(LocatorTriggers.AfterIdle)))
        {
            _effects.Locate();
        }
    }

    public void Typed() => _effects.MarkTyped();

    public void CtrlTapped()
    {
        if (_effects.Current.Cursor.LocatorTriggers.HasFlag(LocatorTriggers.CtrlKey))
        {
            _effects.Locate();
        }
    }
}
