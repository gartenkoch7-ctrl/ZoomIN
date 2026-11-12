using ZoomIN.Core.Islands;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Monitors;

/// <summary>
/// One frozen monitor (G2): what its view keeps while the user works elsewhere. Level, mode, lens
/// and dock are taken at the moment of freezing, so zoom commands on other monitors (or zoom off)
/// do not change it. Immutable except for the anchor, which the render thread fills once. Each
/// freeze is a new object: the render thread tells a new freeze from the old one by reference.
/// </summary>
/// <param name="id">Monitor id (device name).</param>
/// <param name="bounds">Physical desktop pixels at the moment of freezing; the area the fence forbids.</param>
/// <param name="level">The user's level on that monitor (temporary levels such as the overview do not count).</param>
/// <param name="still">Show the image of the moment of freezing instead of the live desktop.</param>
/// <param name="frameColor">Colour of the border that marks the monitor as frozen.</param>
/// <param name="pointerInside">Called once, on a render thread, if the pointer gets onto the frozen monitor anyway
/// (the fence failed): the owner then lifts the freeze, as nothing there would match what clicks hit.</param>
/// <param name="islands">The islands at the moment of freezing (M19 B1), without an edit view: they stay and freeze
/// with the monitor; null = none.</param>
public sealed class FrozenMonitor(
    string id, PhysicalRect bounds, double level, ZoomMode mode, LensSize lens, DockLayout dock, bool still, RgbColor frameColor,
    Action<FrozenMonitor>? pointerInside = null, IslandScene? islands = null)
{
    // The anchor is two doubles (not atomic): written once, then published through the state.
    private const int Empty = 0;
    private const int Writing = 1;
    private const int Kept = 2;
    private int _anchorState;
    private double _anchorX;
    private double _anchorY;
    private int _stillClaimed;
    private bool _stillUnavailable;

    public string Id { get; } = id ?? throw new ArgumentNullException(nameof(id));

    public PhysicalRect Bounds { get; } = bounds;

    public double Level { get; } = ZoomLimits.Clamp(level);

    public ZoomMode Mode { get; } = mode;

    public LensSize Lens { get; } = lens ?? throw new ArgumentNullException(nameof(lens));

    public DockLayout Dock { get; } = dock ?? throw new ArgumentNullException(nameof(dock));

    public bool Still { get; } = still;

    public RgbColor FrameColor { get; } = frameColor;

    /// <summary>The islands as they were when the monitor froze; later changes wait for the thaw (B1).</summary>
    public IslandScene? Islands { get; } = islands is { Edit: not null } ? islands with { Edit = null } : islands;

    /// <summary>The number Windows shows for the monitor (for announcements).</summary>
    public int Number => MonitorNames.Number(Id);

    /// <summary>"still" or "live": the value of log 3310 and the tag <c>zoomin.freeze.image</c>.</summary>
    public string ImageKind => Still ? "still" : "live";

    /// <summary>
    /// Keeps the view's anchor at the moment of freezing. The first call wins: a render pipeline
    /// rebuilt while frozen (display change) shows the same view again instead of the monitor's middle.
    /// </summary>
    /// <remarks>Called on a render thread: no allocation.</remarks>
    private void KeepAnchor(PhysicalPoint anchor)
    {
        if (Interlocked.CompareExchange(ref _anchorState, Writing, Empty) == Empty)
        {
            _anchorX = anchor.X;
            _anchorY = anchor.Y;
            Volatile.Write(ref _anchorState, Kept);
        }
    }

    /// <summary>
    /// Claims the one still image of this freeze: true only the first time. The image lives on one
    /// pipeline's GPU device; a pipeline rebuilt while frozen must not take a new one of a later desktop.
    /// </summary>
    public bool ClaimStill() => Interlocked.Exchange(ref _stillClaimed, 1) == 0;

    /// <summary>The render thread saw the pointer arrive on this frozen monitor (once per arrival).</summary>
    public void ReportPointerInside() => pointerInside?.Invoke(this);

    /// <summary>The still image could not be taken or kept: the monitor shows live (and at 1× needs no copy).</summary>
    public bool StillUnavailable => Volatile.Read(ref _stillUnavailable);

    public void MarkStillUnavailable() => Volatile.Write(ref _stillUnavailable, true);

    /// <summary>
    /// The anchor of the frozen view: the kept one, or else <paramref name="last"/> (the anchor of the
    /// last live frame), which is then kept. Per frame on a render thread: lock-free, 0 B.
    /// </summary>
    public PhysicalPoint AnchorOr(PhysicalPoint last)
    {
        if (TryGetAnchor(out var anchor))
        {
            return anchor;
        }

        KeepAnchor(last);
        return TryGetAnchor(out anchor) ? anchor : last;
    }

    /// <summary>The kept anchor; per frame, lock-free, 0 B.</summary>
    private bool TryGetAnchor(out PhysicalPoint anchor)
    {
        // The state is written last (release) and read first (acquire): a kept anchor is whole.
        var kept = Volatile.Read(ref _anchorState) == Kept;
        anchor = kept ? new PhysicalPoint(_anchorX, _anchorY) : default;
        return kept;
    }
}
