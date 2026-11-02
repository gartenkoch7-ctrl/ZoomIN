using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Pointer;

/// <summary>
/// Software rendering of the styled pointer for the live preview on the pointer page. Uses the
/// same rules as Cursor.hlsl (colour mode, rim from dilated alpha), so the preview matches.
/// </summary>
public static class PointerPreview
{
    // Rec. 709 luma weights, as in the colour filters.
    private const double Lr = 0.2126;
    private const double Lg = 0.7152;
    private const double Lb = 0.0722;

    /// <summary>Returns premultiplied BGRA pixels of the scaled pointer plus rim.</summary>
    public static (byte[] Pixels, int Width, int Height) Render(CursorShape shape, PointerStyle style, double scale)
    {
        var rim = style.RimPixelsFor(scale);
        var texel = scale / shape.Density; // screen pixels per image pixel
        var width = (int)Math.Ceiling((shape.Width * texel) + (2 * rim));
        var height = (int)Math.Ceiling((shape.Height * texel) + (2 * rim));
        var pixels = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var sx = (x - rim) / texel;
                var sy = (y - rim) / texel;
                var (b, g, r, a) = Colored(Sample(shape, sx, sy), style);
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
    public static (double B, double G, double R, double A) Colored((double B, double G, double R, double A) p, PointerStyle style)
    {
        if (p.A <= 0 || style.ColorMode == PointerColorMode.Original)
        {
            return p;
        }

        if (style.ColorMode == PointerColorMode.Inverted)
        {
            return (p.A - p.B, p.A - p.G, p.A - p.R, p.A);
        }

        // Tint: brightness decides how much of the colour shows; black outlines stay black.
        var luma = ((Lr * p.R) + (Lg * p.G) + (Lb * p.B)) / p.A;
        var c = style.Color;
        return (c.B * luma * p.A / 255, c.G * luma * p.A / 255, c.R * luma * p.A / 255, p.A);
    }

    private static (double B, double G, double R, double A) Sample(CursorShape shape, double x, double y)
    {
        var ix = (int)Math.Floor(x);
        var iy = (int)Math.Floor(y);
        if (ix < 0 || iy < 0 || ix >= shape.Width || iy >= shape.Height)
        {
            return default;
        }

        var i = ((iy * shape.Width) + ix) * 4;
        return (shape.Pixels[i], shape.Pixels[i + 1], shape.Pixels[i + 2], shape.Pixels[i + 3]);
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
