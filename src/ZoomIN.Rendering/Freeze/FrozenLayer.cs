using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Still;

namespace ZoomIN.Rendering.Freeze;

/// <summary>
/// What a frozen monitor (G2) needs on its render thread: the anchor kept from the moment of
/// freezing, the still image if the user chose one (building block "Standbild", no new capture frame
/// is read while it is held), and the border that marks the monitor as frozen. Per frame: no
/// allocation; logs and the still copy only when a freeze starts or ends.
/// </summary>
internal sealed class FrozenLayer : IDisposable
{
    // Thicker than the lens frame (4 px) and drawn inside the monitor, so it is clearly a state.
    private const double BorderWidth = 6;

    private readonly CursorPass _pass;
    private readonly StillFrame _still;
    private readonly string _monitorId;
    private readonly ILogger _log;
    private FrozenMonitor? _current;
    private FrozenMonitor? _stillFor;
    private WindowRegions? _stillWindows;
    private bool _pointerLeft;
    private bool _pointerOnFrozen;

    public FrozenLayer(CursorPass pass, StillFrame still, string monitorId, ILogger log)
    {
        _pass = pass;
        _still = still;
        _monitorId = monitorId;
        _log = log;
    }

    /// <summary>
    /// Call first in every frame: notices a freeze starting or ending and gives back a still image
    /// that belongs to an earlier freeze.
    /// </summary>
    public void Begin(FrozenMonitor? frozen)
    {
        if (ReferenceEquals(frozen, _current))
        {
            return;
        }

        if (_current is not null)
        {
            _log.FrozenViewEnded(_monitorId);
        }

        if (frozen is not null)
        {
            _log.FrozenViewStarted(_monitorId, frozen.ImageKind);
        }

        _current = frozen;
        _pointerOnFrozen = false;
        if (_stillFor is not null)
        {
            _still.Release();
            _stillFor = null;
            _stillWindows = null;
        }
    }

    /// <summary>
    /// Whether this frame must show the Windows pointer: always while the pointer is on the frozen
    /// monitor (nothing draws it there), and once when the monitor is frozen after the pointer left it.
    /// This monitor may have hidden the system pointer, and the one the pointer jumped to may sleep at
    /// 1× and never show it. Remembered across frames, because the jump comes before the freeze is published.
    /// </summary>
    /// <param name="wasInside">The pointer was drawn on this monitor in the frame before.</param>
    /// <param name="onMonitor">The pointer is on this monitor now.</param>
    public bool NeedsSystemPointer(bool wasInside, bool onMonitor)
    {
        _pointerLeft |= wasInside && !onMonitor;
        if (_current is null)
        {
            _pointerLeft &= !onMonitor;
            return false;
        }

        // The fence should keep it out; if it failed, the owner may end the freeze (once per arrival).
        if (onMonitor && !_pointerOnFrozen)
        {
            _current.ReportPointerInside();
        }

        _pointerOnFrozen = onMonitor;

        var show = onMonitor || _pointerLeft;
        _pointerLeft = false;
        return show;
    }

    /// <summary>The still image of this freeze is held: the frame reads no new capture frame.</summary>
    public bool HoldsStill(FrozenMonitor? frozen) => frozen is not null && ReferenceEquals(frozen, _stillFor);

    /// <summary>Takes the still image of a freeze that wants one, once; true if a new image was taken.</summary>
    /// <param name="current">The desktop copy shows the desktop as it is now; after Win+L or UAC it waits for the next frame.</param>
    /// <param name="windows">The window regions now: the filter per window stays with the windows in the image.</param>
    public bool TryCapture(FrozenMonitor? frozen, ID3D11DeviceContext context, ID3D11Texture2D desktop, bool current, WindowRegions windows)
    {
        if (frozen is not { Still: true, StillUnavailable: false } || !current || ReferenceEquals(frozen, _stillFor))
        {
            return false;
        }

        // A pipeline rebuilt while frozen (failure, display change) cannot get the old image back and
        // must not pass a newer desktop off as it: then the monitor stays live, as after a failed copy.
        if (!frozen.ClaimStill() || !_still.Capture(context, desktop))
        {
            // Tried again with the next freeze, not every frame.
            frozen.MarkStillUnavailable();
            _log.StillNotTaken(_monitorId);
            return false;
        }

        _stillFor = frozen;
        _stillWindows = windows;
        return true;
    }

    /// <summary>Window regions for the filter per window: those of the still image while it is held.</summary>
    public WindowRegions Windows(WindowRegions live) => _stillFor is not null && _stillWindows is { } held ? held : live;

    /// <summary>The image to magnify: the held still image, or <paramref name="live"/>.</summary>
    public ID3D11ShaderResourceView Desktop(ID3D11ShaderResourceView live) =>
        _stillFor is not null && _still.ShaderResource is { } still ? still : live;

    /// <summary>The border around a frozen monitor, in its frame colour.</summary>
    public void Draw(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor, FrozenMonitor? frozen)
    {
        if (frozen is null || monitor.Width <= 0 || monitor.Height <= 0)
        {
            return;
        }

        try
        {
            _pass.DrawRect(context, target, 0, 0, (float)monitor.Width, (float)monitor.Height, CursorConstants.ForRect(
                (float)(BorderWidth / monitor.Width), (float)(BorderWidth / monitor.Height), FrameColor.From(frozen.FrameColor)));
        }
        finally
        {
            _pass.End(context);
        }
    }

    public void Dispose() => _still.Dispose();
}
