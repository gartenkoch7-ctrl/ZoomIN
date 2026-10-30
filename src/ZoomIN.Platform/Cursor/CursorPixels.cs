namespace ZoomIN.Platform.Cursor;

/// <summary>Converts Windows pointer bitmaps (BGRA, top-down) into premultiplied BGRA.</summary>
internal static class CursorPixels
{
    /// <summary>
    /// Monochrome pointer: AND mask on top, XOR mask below. AND 0 / XOR 0 = black,
    /// AND 0 / XOR 1 = white, AND 1 / XOR 0 = transparent, AND 1 / XOR 1 = "invert screen",
    /// which an overlay cannot do – drawn black; the contrast rim keeps it visible on dark areas.
    /// </summary>
    public static byte[] FromMonochrome(byte[] mask, int width, int height)
    {
        var result = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var and = mask[i * 4] != 0;
            var xor = mask[(i + (width * height)) * 4] != 0;
            if (and && !xor)
            {
                continue;
            }

            var value = (byte)(!and && xor ? 255 : 0);
            Set(result, i, value, value, value, 255);
        }

        return result;
    }

    /// <summary>
    /// Colour pointer. Modern pointers carry alpha; old ones have alpha 0 everywhere and use the
    /// AND mask for transparency instead.
    /// </summary>
    public static byte[] FromColor(byte[] color, byte[] mask, int width, int height)
    {
        var hasAlpha = false;
        for (var i = 3; i < color.Length; i += 4)
        {
            if (color[i] != 0)
            {
                hasAlpha = true;
                break;
            }
        }

        var result = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var alpha = hasAlpha ? color[(i * 4) + 3] : (byte)(mask[i * 4] == 0 ? 255 : 0);
            Set(result, i, Premultiply(color[i * 4], alpha), Premultiply(color[(i * 4) + 1], alpha), Premultiply(color[(i * 4) + 2], alpha), alpha);
        }

        return result;
    }

    // Pointers with real alpha are already premultiplied by Windows; values never exceed alpha.
    private static byte Premultiply(byte channel, byte alpha) => Math.Min(channel, alpha);

    private static void Set(byte[] pixels, int index, byte b, byte g, byte r, byte a)
    {
        pixels[index * 4] = b;
        pixels[(index * 4) + 1] = g;
        pixels[(index * 4) + 2] = r;
        pixels[(index * 4) + 3] = a;
    }
}
