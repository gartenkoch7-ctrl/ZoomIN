using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using ZoomIN.Rendering.Capture;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Decides per frame whether the desktop of one monitor is captured. Zoom and filters always need
/// it; without them Windows shows the desktop and only the overlay is drawn, unless the adaptive
/// pointer (M15, ADR-0026) needs to see what is under it on the monitor with the mouse.
/// Runs every frame: no allocations; logs once per pipeline.
/// </summary>
internal sealed class DesktopFeed(DesktopDuplicator duplicator, string deviceName, ILogger log)
{
    private bool _loggedUnzoomed;

    /// <summary>The captured desktop, or null if this frame captures nothing.</summary>
    public ID3D11ShaderResourceView? Desktop { get; private set; }

    /// <summary>
    /// The desktop is needed: always while zoomed or filtered; at 1× only for the adaptive pointer
    /// on the monitor with the mouse (other monitors would copy for nothing).
    /// </summary>
    public static bool Needed(in FrameRequest request, bool pointerHere) =>
        !request.OverlayOnly || (request.Effects.Cursor.NeedsDesktop && pointerHere);

    /// <summary>Captures if this frame needs the desktop.</summary>
    /// <param name="pointerHere">The mouse is on this monitor (read once per frame by the caller).</param>
    /// <param name="newImage">Desktop Duplication delivered a new image.</param>
    /// <returns>False if the desktop is needed but nothing was captured yet: draw nothing.</returns>
    public bool Update(in FrameRequest request, bool pointerHere, out bool newImage)
    {
        newImage = false;
        Desktop = null;
        if (!Needed(request, pointerHere))
        {
            return true;
        }

        if (request.OverlayOnly && !_loggedUnzoomed)
        {
            _loggedUnzoomed = true;
            log.AdaptivePointerCapture(deviceName);
        }

        newImage = duplicator.Update();
        if (!duplicator.HasImage)
        {
            return false;
        }

        Desktop = duplicator.ShaderResource;
        return true;
    }
}
