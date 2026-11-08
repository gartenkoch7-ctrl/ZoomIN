using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Pointer;

/// <summary>
/// Software rendering of the styled pointer for the live preview on the pointer page. Uses the
/// same rules as Cursor.hlsl (colour mode, rim from dilated alpha), so the preview matches.
/// </summary>
public static class PointerPreview
{
    /// <summary>Returns premultiplied BGRA pixels of the scaled pointer plus rim.</summary>
    /// <param name="shape">Pointer image.</param>
    /// <param name="style">Pointer settings.</param>
    /// <param name="scale">Pointer size.</param>
    /// <param name="leftLuma">Brightness 0–1 of the background under the left half (adaptive mode).</param>
    /// <param name="rightLuma">Brightness 0–1 under the right half: a light/dark edge in the middle.</param>
    public static (byte[] Pixels, int Width, int Height) Render(
        CursorShape shape, PointerStyle style, double scale, double leftLuma = 1, double rightLuma = 1)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(style);
        var rim = style.RimPixelsFor(scale);
        var texel = scale / shape.Density; // screen pixels per image pixel
        var width = (int)Math.Ceiling((shape.Width * texel) + (2 * rim));
        var height = (int)Math.Ceiling((shape.Height * texel) + (2 * rim));
        var pixels = new byte[width * height * 4];
        var image = shape.PixelsFor(style.ColorMode);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var sx = (x - rim) / texel;
                var sy = (y - rim) / texel;
                var background = x < width / 2 ? leftLuma : rightLuma;
                var (b, g, r, a) = Colored(Sample(shape, image, sx, sy), style, background);
                if (rim > 0 && a < 255)
                {
                    var near = NearAlpha(shape, sx, sy, rim / texel);
                    var c = style.RimColor;
                    var keep = 1 - (a / 255.0);
                    (b, g, r, a) = (b + (c.B * near * keep), g + (c.G * near * keep), r + (c.R * near * keep), a + (255 * near * keep));
                }

                var i = ((y * width) + x) * 4;
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (Byte(b), Byte(g), Byte(r), Byte(a));
            }
        }

        return (pixels, width, height);
    }

    /// <summary>Applies the colour mode to one premultiplied pixel.</summary>
    /// <param name="p">Premultiplied pixel.</param>
    /// <param name="style">Pointer settings.</param>
    /// <param name="backgroundLuma">Brightness 0–1 shown under the pixel; only the adaptive mode uses it.</param>
    public static (double B, double G, double R, double A) Colored(
        (double B, double G, double R, double A) p, PointerStyle style, double backgroundLuma = 1)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (p.A <= 0 || style.ColorMode == PointerColorMode.Original)
        {
            return p;
        }

        if (style.ColorMode == PointerColorMode.Adaptive)
        {
            var own = AdaptiveColor.Luma(p.R, p.G, p.B) * 255 / p.A;
            var (cr, cg, cb) = AdaptiveColor.Pick(own, backgroundLuma, style);
            return (cb * p.A / 255, cg * p.A / 255, cr * p.A / 255, p.A);
        }

        if (style.ColorMode == PointerColorMode.Inverted)
        {
            return (p.A - p.B, p.A - p.G, p.A - p.R, p.A);
        }

        // Tint: brightness decides how much of the colour shows; black outlines stay black.
        var luma = AdaptiveColor.Luma(p.R, p.G, p.B) * 255 / p.A;
        var c = style.Color;
        return (c.B * luma * p.A / 255, c.G * luma * p.A / 255, c.R * luma * p.A / 255, p.A);
    }

    private static (double B, double G, double R, double A) Sample(CursorShape shape, double x, double y) =>
        Sample(shape, shape.Pixels, x, y);

    private static (double B, double G, double R, double A) Sample(CursorShape shape, byte[] pixels, double x, double y)
    {
        var ix = (int)Math.Floor(x);
        var iy = (int)Math.Floor(y);
        if (ix < 0 || iy < 0 || ix >= shape.Width || iy >= shape.Height)
        {
            return default;
        }

        var i = ((iy * shape.Width) + ix) * 4;
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    private static double NearAlpha(CursorShape shape, double x, double y, double radius)
    {
        var near = 0.0;
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4;
            near = Math.Max(near, Sample(shape, x + (Math.Cos(angle) * radius), y + (Math.Sin(angle) * radius)).A / 255);
        }

        return near;
    }

    private static byte Byte(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
}
