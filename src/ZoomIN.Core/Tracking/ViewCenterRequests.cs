using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Requests "centre the view of monitor M on desktop point P at zoom Z" from features to the
/// render threads (M17: the overview zooms in at its frame and returns to the previous place on
/// Esc). The render thread hands <see cref="PlacementFor"/> to <see cref="AnchorController"/> as
/// <see cref="AnchorInput.Placement"/>, which treats a newer <see cref="ViewPlacement.At"/> as a new
/// request. Latest request wins. Writers: any thread; render threads read lock-free, 0 B.
/// </summary>
public sealed class ViewCenterRequests
{
    private readonly Lock _gate = new();
    private ViewCenterRequest? _latest;

    /// <summary>The latest request, or null (for the KI-Debug state and tests).</summary>
    public ViewCenterRequest? Latest => Volatile.Read(ref _latest);

    /// <param name="zoom">The level the zoom glides to, which the view is placed for.</param>
    /// <param name="at">Stopwatch timestamp of the request (<see cref="TimeProvider.GetTimestamp"/> of the system clock).</param>
    public void Request(string monitorId, PhysicalPoint center, double zoom, long at)
    {
        ArgumentNullException.ThrowIfNull(monitorId);

        // Never older than the request before, so a newer request is always news for the anchor.
        lock (_gate)
        {
            var previous = Volatile.Read(ref _latest)?.Placement.At ?? 0;
            Volatile.Write(ref _latest, new ViewCenterRequest(monitorId, new ViewPlacement(center, zoom, Math.Max(at, previous + 1))));
        }
    }

    /// <summary>The latest request if it is for <paramref name="monitorId"/>, else none. Per frame: lock-free, 0 B.</summary>
    public ViewPlacement PlacementFor(string monitorId) =>
        Volatile.Read(ref _latest) is { } request && request.MonitorId == monitorId ? request.Placement : default;
}
