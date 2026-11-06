using System.Globalization;
using System.Text;
using ZoomIN.Core.Filters;
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
    public static string Describe(string monitor, PhysicalRect bounds, PhysicalRect source, in FrameRequest request, bool smartWeights)
    {
        var effects = request.Effects;
        var text = new StringBuilder();
        Line(text, $"Monitor: {monitor} {Rect(bounds)}");
        Line(text, $"Mode: {request.Mode}, zoom {request.Zoom:0.00}, source {Rect(source)}");
        Line(text, $"Monitor filter: {(effects.Filter.Enabled ? effects.Filter.Filter.ToString() : "off")}, smart {effects.IsSmart}, "
            + $"protect photos {effects.ProtectPhotos}, contrast {effects.Filter.Contrast:0.00}, brightness {effects.Filter.Brightness:0.00}");
        Line(text, $"Smart weights in this frame: {smartWeights}");
        Line(text, $"Window regions (front to back): {request.Windows.Count}, dropped {request.Windows.Dropped}, filtered here {request.WindowsFiltered}");
        var rects = request.Windows.Rects;
        var palette = request.Windows.Palette;
        for (var i = 0; i < rects.Length; i++)
        {
            Line(text, $"  {i,2}: {Rect(rects[i])} -> {PaletteName(palette[i])}");
        }

        return text.ToString();
    }

    private static string PaletteName(byte index) => index switch
    {
        WindowPalette.Monitor => "monitor filter",
        WindowPalette.Original => "original",
        _ => ((ColorFilter)(index - 2)).ToString(),
    };

    private static string Rect(PhysicalRect r) =>
        string.Create(CultureInfo.InvariantCulture, $"({r.X:0},{r.Y:0} {r.Width:0}x{r.Height:0})");

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');
}
