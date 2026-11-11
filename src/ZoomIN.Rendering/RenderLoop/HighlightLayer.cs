using Vortice.Direct3D11;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Focus frame (magenta, M6) and caret marker (shape, colour and thickness from the profile, E2),
/// drawn on the magnified image and faded under their show conditions (E1). Runs every frame: no
/// allocations.
/// </summary>
internal sealed class HighlightLayer
{
    private const double FrameWidth = 3;
    private const double FrameGap = 2;

    private readonly CursorPass _pass;
    private readonly HighlightMetrics _metrics;

    public HighlightLayer(CursorPass pass, HighlightMetrics metrics)
    {
        _pass = pass;
        _metrics = metrics;
    }

    /// <param name="alphas">Opacity per highlight from its show condition (E1).</param>
    public void Draw(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        in ModeFrame frame, TrackingSnapshot tracking, TrackingOptions options, MarkerAlphas alphas)
    {
        if (!options.Enabled)
        {
            return;
        }

        try
        {
            DrawMarks(context, target, monitor, frame, tracking, options, alphas.Focus, alphas.Caret);
        }
        finally
        {
            _pass.End(context);
        }
    }

    private void DrawMarks(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        in ModeFrame frame, TrackingSnapshot tracking, TrackingOptions options, double focusAlpha, double caretAlpha)
    {
        if (options.FocusFrame && focusAlpha > 0 && tracking.Focus is { } focus && FrameMapping.Intersects(focus, frame.Source))
        {
            var r = Expand(FrameMapping.Map(frame, focus), FrameGap + FrameWidth);
            _pass.DrawRect(context, target, (float)(r.X - monitor.X), (float)(r.Y - monitor.Y), (float)r.Width, (float)r.Height,
                CursorConstants.ForRect((float)(FrameWidth / r.Width), (float)(FrameWidth / r.Height), FrameColor.From(RgbColor.Magenta, focusAlpha)));
        }

        if (caretAlpha > 0 && CaretMarkerGeometry.TryGetCaret(tracking, options, out var caret))
        {
            DrawCaret(context, target, monitor, frame, caret, options.CaretStyle, caretAlpha);
        }
    }

    private void DrawCaret(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        in ModeFrame frame, PhysicalRect caret, CaretMarkerStyle style, double alpha)
    {
        // The visible area: the whole monitor in fullscreen, otherwise lens or strip.
        var shown = FrameMapping.Map(frame, caret);
        if (!CaretMarkerGeometry.InView(shown, frame.Target))
        {
            return;
        }

        var parts = CaretMarkerGeometry.Compute(shown, FrameMapping.Scale(frame), style.Shape, style.Thickness, frame.Target);
        var color = FrameColor.From(style.Color, CaretMarkerStyle.Opacity * alpha);
        var a = parts.First;
        var x = (float)(a.X - monitor.X);
        var y = (float)(a.Y - monitor.Y);
        switch (parts.Shape)
        {
            case CaretMarkerShape.Wedges:
                var b = parts.Second;
                _pass.DrawWedge(context, target, x, y, (float)a.Width, (float)a.Height, CursorConstants.ForWedge(false, color));
                _pass.DrawWedge(context, target, (float)(b.X - monitor.X), (float)(b.Y - monitor.Y), (float)b.Width, (float)b.Height,
                    CursorConstants.ForWedge(true, color));
                break;
            case CaretMarkerShape.Frame:
                _pass.DrawRect(context, target, x, y, (float)a.Width, (float)a.Height,
                    CursorConstants.ForRect((float)(parts.Line / a.Width), (float)(parts.Line / a.Height), color));
                break;
            case CaretMarkerShape.Circle:
                var radius = a.Width / 2;
                _pass.DrawRing(context, target, (float)(a.X + radius - monitor.X), (float)(a.Y + radius - monitor.Y), (float)radius,
                    CursorConstants.ForRing((float)Math.Min(1, parts.Line / radius), color));
                break;
            default:
                _pass.DrawRect(context, target, x, y, (float)a.Width, (float)a.Height, CursorConstants.ForRect(0.5f, 0.5f, color));
                break;
        }

        _metrics.CaretDrawn(parts.Shape);
    }

    private static PhysicalRect Expand(PhysicalRect r, double by) =>
        new(r.X - by, r.Y - by, r.Width + (2 * by), r.Height + (2 * by));
}
