using System.Diagnostics;
using ZoomIN.Core.Monitors;

namespace ZoomIN.App.Freeze;

/// <summary>
/// The open <c>zoomin.view.freeze</c> operations per freeze. Each started one is stopped exactly once,
/// so the gauge <c>zoomin.view.frozen_monitors</c> never drifts. Used under the controller's lock.
/// </summary>
internal sealed class FreezeOperations
{
    private readonly Dictionary<FrozenMonitor, Activity?> _open = [];

    public void Start(FrozenMonitor monitor) => _open[monitor] = FreezeOperation.Start(monitor);

    public void Stop(IEnumerable<FrozenMonitor> monitors, string reason)
    {
        foreach (var monitor in monitors)
        {
            if (_open.Remove(monitor, out var operation))
            {
                FreezeOperation.Stop(operation, reason);
            }
        }
    }

    /// <summary>Stops every open one, also of freezes a failed step already removed from the model.</summary>
    public void StopAll(string reason) => Stop([.. _open.Keys], reason);
}
