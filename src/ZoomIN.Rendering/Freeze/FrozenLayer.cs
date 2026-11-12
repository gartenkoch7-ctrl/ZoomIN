using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;
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
    private FrozenMonitor? _stillFailedFor;

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
            _log.FrozenViewStarted(_monitorId, frozen.Still ? "still" : "live");
        }

        _current = frozen;
        if (_stillFor is not null)
        {
            _still.Release();
            _stillFor = null;
        }
    }

    /// <summary>
    /// The anchor of the frozen view: the one kept for this freeze, or else <paramref name="last"/>
    /// (the anchor of the last live frame), which is then kept.
    /// </summary>
    public static PhysicalPoint Anchor(FrozenMonitor frozen, PhysicalPoint last)
    {
        if (!frozen.TryGetAnchor(out var anchor))
        {
            frozen.KeepAnchor(last);
            frozen.TryGetAnchor(out anchor);
        }

        return anchor;
    }

    /// <summary>The still image of this freeze is held: the frame reads no new capture frame.</summary>
    public bool HoldsStill(FrozenMonitor? frozen) => frozen is not null && ReferenceEquals(frozen, _stillFor);

    /// <summary>Takes the still image of a freeze that wants one, once; true if a new image was taken.</summary>
    public bool TryCapture(FrozenMonitor? frozen, ID3D11DeviceContext context, ID3D11Texture2D desktop)
    {
        if (frozen is not { Still: true } || ReferenceEquals(frozen, _stillFor) || ReferenceEquals(frozen, _stillFailedFor))
        {
            return false;
        }

        if (!_still.Capture(context, desktop))
        {
            // Shown live instead; tried again with the next freeze, not every frame.
            _stillFailedFor = frozen;
            _log.StillNotTaken(_monitorId);
            return false;
        }

        _stillFor = frozen;
        return true;
    }

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
