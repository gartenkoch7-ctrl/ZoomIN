using System.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Picks up new <see cref="ViewPlacement"/> requests for <see cref="AnchorController"/> (M17 B2)
/// and says how long the placed anchor must hold: while the zoom glides to the placement's level
/// it must not be re-anchored at the pointer. Per render thread; no allocations.
/// </summary>
internal sealed class PlacementTracker
{
    // The zoom glide advances every few frames; this long without progress means it stopped.
    private const double StallSeconds = 0.25;

    private long _seenAt;
    private long _since;
    private double _zoom;
    private double _gap;

    /// <summary>True if the zoom step of this frame belongs to the glide to a placed view (including its last step).</summary>
    public bool HeldThisFrame { get; private set; }

    private bool Holding => _zoom > 0;

    /// <summary>Only the margin keeps a placed view; elsewhere the mouse decides at once (see <see cref="ViewPlacement"/>).</summary>
    public static bool Applies(in AnchorInput input) =>
        input.Options.MouseFollow == MouseFollowMode.Margin && input.Mode != ZoomMode.Docked;

    /// <summary>Requests from before this monitor started rendering are not news.</summary>
    public void Initialize(long at) => _seenAt = at;

    /// <summary>Call once per frame. Returns the anchor of a new placement on this monitor, if there is one.</summary>
    public bool TryTake(in AnchorInput input, out PhysicalPoint anchor)
    {
        var placement = input.Placement;
        var taken = false;
        anchor = default;
        if (placement.At > _seenAt)
        {
            _seenAt = placement.At;
            if (input.Monitor.Contains(placement.Center) && double.IsFinite(placement.Zoom))
            {
                anchor = ViewCentering.AnchorFor(input.Mode, input.Monitor, placement.Center, placement.Zoom, input.Lens, input.Dock);
                _zoom = placement.Zoom;
                _gap = Math.Abs(input.Zoom - placement.Zoom);
                taken = true;
            }
        }

        if (taken)
        {
            _since = input.Now;
        }

        HeldThisFrame = Holding;
        Update(input.Zoom, input.Now);
        return taken;
    }

    // Holds until the zoom arrives, turns away from the placement (another zoom change) or makes no
    // progress for a moment: it stopped short (clamped) or never started. Single frames without a
    // step (two renders within one animation tick) do not end it.
    private void Update(double zoom, long now)
    {
        if (!Holding)
        {
            return;
        }

        var gap = Math.Abs(zoom - _zoom);
        if (gap < _gap)
        {
            _since = now;
        }

        var stalled = (now - _since) / (double)Stopwatch.Frequency > StallSeconds;
        var ended = gap < 1e-6 || gap > _gap || stalled;
        _gap = gap;
        _zoom = ended ? 0 : _zoom;
    }
}
