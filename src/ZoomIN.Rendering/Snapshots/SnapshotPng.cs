using System.Buffers.Binary;
using System.IO.Compression;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Snapshots;

/// <summary>
/// Cuts and scales debug snapshot PNGs for the KI-Debug link (ADR-0027). Reads only what
/// <see cref="PngWriter"/> writes (8-bit RGBA, filter "none"), so no imaging library is needed.
/// </summary>
public static class SnapshotPng
{
    /// <summary>
    /// The part <paramref name="crop"/> of the image (clipped to it), scaled down to at most
    /// <paramref name="maxWidth"/> pixels wide. Returns the input unchanged if there is nothing to do.
    /// </summary>
    /// <exception cref="InvalidDataException">Not a PNG written by ZoomIN, or the crop lies outside the image.</exception>
    public static byte[] Reframe(byte[] png, PhysicalRect? crop, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (crop is null && maxWidth <= 0)
        {
            return png;
        }

        var image = Read(png);
        if (crop is { } c)
        {
            image = Crop(image, c);
        }

        if (maxWidth > 0 && image.Width > maxWidth)
        {
            image = Shrink(image, maxWidth);
        }

        using var output = new MemoryStream();
        PngWriter.Write(output, image);
        return output.ToArray();
    }

    internal static SnapshotImage Read(byte[] png)
    {
        if (png.Length < 33 || !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            throw new InvalidDataException("Not a PNG");
        }

        int width = 0, height = 0;
        using var idat = new MemoryStream();
        var at = 8;
        while (at + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            var type = png.AsSpan(at + 4, 4);
            if (length < 0 || at + 12 + length > png.Length)
            {
                throw new InvalidDataException("Broken PNG chunk");
            }

            var data = png.AsSpan(at + 8, length);
            if (type.SequenceEqual("IHDR"u8))
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                if (data[8] != 8 || data[9] != 6 || data[12] != 0)
                {
                    throw new InvalidDataException("Only 8-bit RGBA PNGs without interlace");
                }
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                idat.Write(data);
            }

            at += 12 + length;
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("PNG without size");
        }

        var stride = width * 4;
        var rgba = new byte[stride * height];
        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        for (var y = 0; y < height; y++)
        {
            if (zlib.ReadByte() != 0)
            {
                throw new InvalidDataException("Only PNG filter 'none'");
            }

            zlib.ReadExactly(rgba, y * stride, stride);
        }

        return new SnapshotImage(width, height, rgba);
    }

    internal static SnapshotImage Crop(SnapshotImage image, PhysicalRect crop)
    {
        var left = Math.Clamp((int)crop.X, 0, image.Width);
        var top = Math.Clamp((int)crop.Y, 0, image.Height);
        var right = Math.Clamp((int)(crop.X + crop.Width), 0, image.Width);
        var bottom = Math.Clamp((int)(crop.Y + crop.Height), 0, image.Height);
        if (right <= left || bottom <= top)
        {
            throw new InvalidDataException($"The crop lies outside the image ({image.Width}x{image.Height})");
        }

        var width = right - left;
        var height = bottom - top;
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            Buffer.BlockCopy(image.Rgba, (((top + y) * image.Width) + left) * 4, rgba, y * width * 4, width * 4);
        }

        return new SnapshotImage(width, height, rgba);
    }

    // Box filter: every target pixel averages the source pixels it covers, so thin text stays readable.
    internal static SnapshotImage Shrink(SnapshotImage image, int maxWidth)
    {
        var width = maxWidth;
        var height = Math.Max(1, (int)Math.Round(image.Height * (double)maxWidth / image.Width));
        var rgba = new byte[width * height * 4];
        Span<int> sum = stackalloc int[4];
        for (var y = 0; y < height; y++)
        {
            var y0 = y * image.Height / height;
            var y1 = Math.Max(y0 + 1, (y + 1) * image.Height / height);
            for (var x = 0; x < width; x++)
            {
                var x0 = x * image.Width / width;
                var x1 = Math.Max(x0 + 1, (x + 1) * image.Width / width);
                sum.Clear();
                for (var sy = y0; sy < y1; sy++)
                {
                    for (var sx = x0; sx < x1; sx++)
                    {
                        var i = ((sy * image.Width) + sx) * 4;
                        sum[0] += image.Rgba[i];
                        sum[1] += image.Rgba[i + 1];
                        sum[2] += image.Rgba[i + 2];
                        sum[3] += image.Rgba[i + 3];
                    }
                }

                var count = (y1 - y0) * (x1 - x0);
                var o = ((y * width) + x) * 4;
                for (var k = 0; k < 4; k++)
                {
                    rgba[o + k] = (byte)(sum[k] / count);
                }
            }
        }

        return new SnapshotImage(width, height, rgba);
    }
}
