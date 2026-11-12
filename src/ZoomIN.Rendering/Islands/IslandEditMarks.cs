using Vortice.Direct3D11;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Islands;

/// <summary>
/// What the island edit mode draws besides the islands (M19 B1): filled handles at the corners of
/// the selected island and the dashed rubber band of a new one. Uses the rectangle shader of the
/// pointer pass; every frame on the render thread, no allocation.
/// </summary>
internal sealed class IslandEditMarks(CursorPass pass)
{
    // Side of a corner handle and of the dashes, in physical pixels.
    private const double Handle = 10;
    private const double DashLine = 2;
    private const double DashLength = 8;
    private const double DashGap = 6;

    // At most this many dashes per side; a longer side gets longer dashes.
    private const int MaxDashes = 64;

    private static readonly FrameColor s_dash = new(1, 1, 1, 1);
    private static readonly FrameColor s_dashShade = new(0, 0, 0, 0.8f);

    public void DrawHandles(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor, PhysicalRect island, FrameColor color)
    {
        Fill(context, target, monitor, new PhysicalRect(island.X - (Handle / 2), island.Y - (Handle / 2), Handle, Handle), color);
        Fill(context, target, monitor, new PhysicalRect(island.Right - (Handle / 2), island.Y - (Handle / 2), Handle, Handle), color);
        Fill(context, target, monitor, new PhysicalRect(island.X - (Handle / 2), island.Bottom - (Handle / 2), Handle, Handle), color);
        Fill(context, target, monitor, new PhysicalRect(island.Right - (Handle / 2), island.Bottom - (Handle / 2), Handle, Handle), color);
    }

    /// <summary>White dashes on a dark line, so the band shows on light and dark content alike.</summary>
    public void DrawRubberBand(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor, PhysicalRect band)
    {
        Fill(context, target, monitor, new PhysicalRect(band.X, band.Y, band.Width, DashLine), s_dashShade);
        Fill(context, target, monitor, new PhysicalRect(band.X, band.Bottom - DashLine, band.Width, DashLine), s_dashShade);
        Fill(context, target, monitor, new PhysicalRect(band.X, band.Y, DashLine, band.Height), s_dashShade);
        Fill(context, target, monitor, new PhysicalRect(band.Right - DashLine, band.Y, DashLine, band.Height), s_dashShade);
        Dashes(context, target, monitor, new PhysicalPoint(band.X, band.Y), band.Width, horizontal: true);
        Dashes(context, target, monitor, new PhysicalPoint(band.X, band.Bottom - DashLine), band.Width, horizontal: true);
        Dashes(context, target, monitor, new PhysicalPoint(band.X, band.Y), band.Height, horizontal: false);
        Dashes(context, target, monitor, new PhysicalPoint(band.Right - DashLine, band.Y), band.Height, horizontal: false);
    }

    public void End(ID3D11DeviceContext context) => pass.End(context);

    private void Dashes(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor, PhysicalPoint start, double length, bool horizontal)
    {
        var period = Math.Max(DashLength + DashGap, length / MaxDashes);
        var dash = period * DashLength / (DashLength + DashGap);
        for (var at = 0.0; at < length; at += period)
        {
            var size = Math.Min(dash, length - at);
            if (horizontal)
            {
                Fill(context, target, monitor, new PhysicalRect(start.X + at, start.Y, size, DashLine), s_dash);
            }
            else
            {
                Fill(context, target, monitor, new PhysicalRect(start.X, start.Y + at, DashLine, size), s_dash);
            }
        }
    }

    // A filled rectangle (line thickness ≥ 0.5 of the rectangle fills it), clipped to the monitor.
    private void Fill(
        ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor, PhysicalRect rect, FrameColor color)
    {
        var left = Math.Max(rect.X, monitor.X);
        var top = Math.Max(rect.Y, monitor.Y);
        var right = Math.Min(rect.Right, monitor.Right);
        var bottom = Math.Min(rect.Bottom, monitor.Bottom);
        if (right <= left || bottom <= top)
        {
            return;
        }

        pass.DrawRect(context, target, (float)(left - monitor.X), (float)(top - monitor.Y), (float)(right - left), (float)(bottom - top),
            CursorConstants.ForRect(1, 1, color));
    }
}
