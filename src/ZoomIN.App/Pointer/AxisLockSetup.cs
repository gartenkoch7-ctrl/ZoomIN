using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.App.Pointer;

/// <summary>
/// The axis lock's part of <see cref="PointerConstraintController"/> (A6): its fence filter and the
/// key watch that feeds it. Called under the controller's lock. "On" is marked before the steps
/// that can fail and cleared only after them, so a failed step always leaves something for
/// <see cref="TurnOff"/> to clean up.
/// </summary>
internal sealed class AxisLockSetup
{
    private readonly IPointerFence _fence;
    private readonly IHoldKeyWatch _keys;
    private readonly Action<bool> _setHeld;
    private bool _on;

    public AxisLockSetup(IPointerFence fence, IHoldKeyWatch keys)
    {
        _fence = fence;
        _keys = keys;
        _setHeld = Axis.SetHeld;
    }

    public AxisLock Axis { get; } = new();

    public void Apply(PointerConstraintOptions wanted, PointerConstraintOptions? before)
    {
        if (!wanted.AxisLock)
        {
            if (_on)
            {
                _keys.Stop();
                _fence.RemoveFilter(Axis);
                Axis.SetHeld(false);
                _on = false;
            }

            return;
        }

        var wasOn = _on;
        _on = true;
        if (!wasOn || before?.AxisLockKey != wanted.AxisLockKey)
        {
            _keys.Start(wanted.AxisLockKey, _setHeld);
        }

        if (!wasOn)
        {
            _fence.AddFilter(Axis);
        }
    }

    /// <summary>Each step on its own through <paramref name="attempt"/>: one failing must not keep the other running.</summary>
    public void TurnOff(Action<Action> attempt)
    {
        if (_on)
        {
            _on = false;
            attempt(_keys.Stop);
            attempt(() => _fence.RemoveFilter(Axis));
        }

        Axis.SetHeld(false);
    }

    /// <summary>After a desktop switch Windows delivered no key events: the key starts over.</summary>
    public void KeysMissed() => _keys.Reset();
}
