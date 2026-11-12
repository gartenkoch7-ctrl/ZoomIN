using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.App.Freeze;

/// <summary>
/// Hands the fence's own recomputations (desktop switch, unlock, display change) to the freeze
/// controller. They arrive on the fence's watcher thread, which the fence may join while releasing
/// (up to 2 s), so the work runs elsewhere (<paramref name="dispatch"/>) and never waits for the
/// controller's lock there. What was frozen, or being frozen, is taken when the event comes: a
/// queued event must not end a freeze the user makes after it.
/// </summary>
internal sealed class FreezeFenceEvents : IDisposable
{
    private readonly IPointerFence _fence;
    private readonly Func<bool> _freezing;
    private readonly MonitorFreeze _frozen;
    private readonly Action<Action> _dispatch;
    private readonly Action<FenceTrigger, FreezeState, bool> _handle;

    public FreezeFenceEvents(
        IPointerFence fence, MonitorFreeze frozen, Func<bool> freezing, Action<Action> dispatch, Action<FenceTrigger, FreezeState, bool> handle)
    {
        (_fence, _frozen, _freezing, _dispatch, _handle) = (fence, frozen, freezing, dispatch, handle);
        _fence.Recomputed += OnRecomputed;
    }

    public void Dispose() => _fence.Recomputed -= OnRecomputed;

    private void OnRecomputed(object? sender, FenceTrigger trigger)
    {
        if (trigger is not (FenceTrigger.DesktopSwitch or FenceTrigger.SessionUnlock or FenceTrigger.DisplayChange))
        {
            return;
        }

        // The flag first: a freeze publishes its state before it clears the flag, so one of the two
        // reads always sees a freeze that is being made right now.
        var freezing = _freezing();
        var seen = _frozen.State;
        _dispatch(() => _handle(trigger, seen, freezing));
    }
}
