using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// The hook side of the fence: answers per move from the current <see cref="FenceDecision"/>,
/// which the fence publishes as a whole. Lock-free, no allocation, never throws (the hook must
/// only compute; Windows removes slow hooks).
/// </summary>
internal sealed class MoveGate(TimeProvider time, HookHeartbeat heartbeat) : IFenceHookTarget
{
    private FenceDecision _decision = FenceDecision.Open;
    private volatile bool _closed;

    /// <summary>Replaces the decision the hook uses from its next move on; ignored after <see cref="CloseForExit"/>.</summary>
    public void Publish(FenceDecision decision)
    {
        Volatile.Write(ref _decision, decision);

        // A change still running when the process began to exit must not restrict again. The full
        // barrier keeps the store above from being reordered after the read of _closed.
        Interlocked.MemoryBarrier();
        if (_closed)
        {
            Volatile.Write(ref _decision, FenceDecision.Open);
        }
    }

    /// <summary>Lets every move through.</summary>
    public void Open() => Volatile.Write(ref _decision, FenceDecision.Open);

    /// <summary>Process exit: every move goes through from now on, whatever is published later.</summary>
    public void CloseForExit()
    {
        _closed = true;
        Interlocked.MemoryBarrier();
        Open();
    }

    public bool OnMove(PhysicalPoint current, PhysicalPoint requested, out PhysicalPoint allowed)
    {
        heartbeat.Beat();
        var decision = Volatile.Read(ref _decision);
        try
        {
            allowed = decision.Decide(time.GetTimestamp(), current, requested);
            if (!double.IsFinite(allowed.X) || !double.IsFinite(allowed.Y))
            {
                // A filter divided by zero: SetCursorPos would turn NaN into (0, 0).
                heartbeat.FilterFailed();
                allowed = decision.Geometry?.Constrain(current, requested) ?? requested;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A broken filter must not stop the pointer (reported by the next check), but the
            // forbidden areas still win.
            heartbeat.FilterFailed();
            allowed = decision.Geometry?.Constrain(current, requested) ?? requested;
        }

        if (allowed == requested)
        {
            return false;
        }

        FenceMetrics.Blocked();
        return true;
    }
}
