using ZoomIN.Platform.Cursor;

namespace ZoomIN.Platform.Tests.Cursor;

/// <summary>Monochrome pointers (AND/XOR masks), e.g. the standard I-beam, and their adaptive image (M15).</summary>
public sealed class CursorPixelsTests
{
    // Four pixels in one row: transparent, black, white, "invert screen".
    private static readonly byte[] s_mask = Mask(and: [1, 0, 0, 1], xor: [0, 0, 1, 1]);

    [Fact]
    public void Monochrome_DrawsInvertPixelsBlack()
    {
        var pixels = CursorPixels.FromMonochrome(s_mask, 4, 1);

        Assert.Equal([0, 0, 0, 0], Pixel(pixels, 0));
        Assert.Equal([0, 0, 0, 255], Pixel(pixels, 1));
        Assert.Equal([255, 255, 255, 255], Pixel(pixels, 2));
        Assert.Equal([0, 0, 0, 255], Pixel(pixels, 3));
    }

    [Fact]
    public void Adaptive_DrawsInvertPixelsWhite_RestUnchanged()
    {
        var pixels = CursorPixels.FromMonochromeAdaptive(s_mask, 4, 1);

        Assert.NotNull(pixels);
        Assert.Equal([0, 0, 0, 0], Pixel(pixels, 0));
        Assert.Equal([0, 0, 0, 255], Pixel(pixels, 1));
        Assert.Equal([255, 255, 255, 255], Pixel(pixels, 2));
        Assert.Equal([255, 255, 255, 255], Pixel(pixels, 3));
    }

    [Fact]
    public void Adaptive_WithoutInvertPixels_IsNull()
    {
        Assert.Null(CursorPixels.FromMonochromeAdaptive(Mask(and: [1, 0], xor: [0, 1]), 2, 1));
    }

    // GetDIBits as 32-bit rows: the AND half on top, the XOR half below; only the first byte counts.
    private static byte[] Mask(byte[] and, byte[] xor)
    {
        var mask = new byte[(and.Length + xor.Length) * 4];
        for (var i = 0; i < and.Length; i++)
        {
            mask[i * 4] = (byte)(and[i] * 255);
            mask[(and.Length + i) * 4] = (byte)(xor[i] * 255);
        }

        return mask;
    }

    private static byte[] Pixel(byte[] pixels, int index) => pixels[(index * 4)..((index * 4) + 4)];
}
