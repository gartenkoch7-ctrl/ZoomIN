using System.Globalization;
using System.Text;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Snapshots;

/// <summary>
/// The text next to the snapshot images: what ZoomIN was asked to draw. Only technical values –
/// rectangles and palette entries, never window titles or other user content (AGENTS.md 2.5).
/// </summary>
internal static class SnapshotInfo
{
    public static string Describe(string monitor, PhysicalRect bounds, in ModeFrame frame, in FrameRequest request, bool smartWeights)
    {
        var effects = request.Effects;
        var text = new StringBuilder();
        Line(text, $"Monitor: {monitor} {RectText.Format(bounds)}");
        // Invariant: an agent reads this text (ADR-0027), "1,00" in German would be ambiguous.
        Line(text, string.Create(CultureInfo.InvariantCulture, $"Mode: {request.Mode}, zoom {request.Zoom:0.00}, source {RectText.Format(frame.Source)}"));
        Line(text, string.Create(CultureInfo.InvariantCulture, $"Monitor filter: {(effects.Filter.Enabled ? effects.Filter.Filter.ToString() : "off")}, smart {effects.IsSmart}, "
            + $"protect photos {effects.ProtectPhotos}, contrast {effects.Filter.Contrast:0.00}, brightness {effects.Filter.Brightness:0.00}"));
        Line(text, $"Smart weights in this frame: {smartWeights}");
        var enhance = effects.Enhancement;
        Line(text, string.Create(CultureInfo.InvariantCulture, $"Smoothing: {effects.Smoothing}, sharpness {enhance.Sharpness * 100:0} %, "
            + $"bold {enhance.BoldPixels:0.0} px, applied in this view {enhance.At(frame, effects.Smoothing).IsActive}"));
        Line(text, $"Window regions (front to back): {request.Windows.Count}, dropped {request.Windows.Dropped}, filtered here {request.WindowsFiltered}");
        var rects = request.Windows.Rects;
        var palette = request.Windows.Palette;
        for (var i = 0; i < rects.Length; i++)
        {
            Line(text, $"  {i,2}: {RectText.Format(rects[i])} -> {WindowPalette.Name(palette[i])}");
        }

        return text.ToString();
    }

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');
}
