using Vortice.Direct3D11;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>Focus frame and caret marker in magenta (M6 decisions), drawn on the magnified image.</summary>
internal sealed class HighlightLayer
{
    private const double FrameWidth = 3;
    private const double FrameGap = 2;
    private const double MinCaretWidth = 4;
    private static readonly FrameColor s_magenta = new(1.0f, 0.0f, 1.0f, 1.0f);
    private static readonly FrameColor s_caret = new(0.85f, 0.0f, 0.85f, 0.85f);

    private readonly CursorPass _pass;

    public HighlightLayer(CursorPass pass)
    {
        _pass = pass;
    }

    public void Draw(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        in ModeFrame frame, TrackingSnapshot tracking, TrackingOptions options)
    {
        if (!options.Enabled)
        {
            return;
        }

        try
        {
            DrawMarks(context, target, monitor, frame, tracking, options);
        }
        finally
        {
            _pass.End(context);
        }
    }

    private void DrawMarks(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        in ModeFrame frame, TrackingSnapshot tracking, TrackingOptions options)
    {
        if (options.FocusFrame && tracking.Focus is { } focus && FrameMapping.Intersects(focus, frame.Source))
        {
            var r = Expand(FrameMapping.Map(frame, focus), FrameGap + FrameWidth);
            _pass.DrawRect(context, target, (float)(r.X - monitor.X), (float)(r.Y - monitor.Y), (float)r.Width, (float)r.Height,
                CursorConstants.ForRect((float)(FrameWidth / r.Width), (float)(FrameWidth / r.Height), s_magenta));
        }

        if (options.CaretMarker && !tracking.CaretEstimated && tracking.Caret is { } caret && FrameMapping.Intersects(caret, frame.Source))
        {
            var r = FrameMapping.Map(frame, caret);
            var width = Math.Max(MinCaretWidth, r.Width);
            _pass.DrawRect(context, target, (float)(r.X - monitor.X - ((width - r.Width) / 2)), (float)(r.Y - monitor.Y),
                (float)width, (float)r.Height, CursorConstants.ForRect(0.5f, 0.5f, s_caret));
        }
    }

    private static PhysicalRect Expand(PhysicalRect r, double by) =>
        new(r.X - by, r.Y - by, r.Width + (2 * by), r.Height + (2 * by));
}
