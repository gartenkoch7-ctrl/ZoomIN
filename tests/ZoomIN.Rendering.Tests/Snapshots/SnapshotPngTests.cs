using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.Tests.Snapshots;

public sealed class SnapshotPngTests
{
    [Fact]
    public void ReadsBackWhatPngWriterWrote()
    {
        var image = Gradient(5, 3);

        var read = SnapshotPng.Read(Encode(image));

        Assert.Equal(5, read.Width);
        Assert.Equal(3, read.Height);
        Assert.Equal(image.Rgba, read.Rgba);
    }

    [Fact]
    public void WithoutCropAndScale_ReturnsTheSameBytes()
    {
        var png = Encode(Gradient(4, 4));

        Assert.Same(png, SnapshotPng.Reframe(png, null, 0));
    }

    [Fact]
    public void Crop_TakesExactlyThePixelsOfTheRectangle()
    {
        var image = Gradient(6, 4);

        var cropped = SnapshotPng.Read(SnapshotPng.Reframe(Encode(image), new PhysicalRect(2, 1, 3, 2), 0));

        Assert.Equal(3, cropped.Width);
        Assert.Equal(2, cropped.Height);
        Assert.Equal(Pixel(image, 2, 1), Pixel(cropped, 0, 0));
        Assert.Equal(Pixel(image, 4, 2), Pixel(cropped, 2, 1));
    }

    [Fact]
    public void Crop_IsClippedToTheImage()
    {
        var cropped = SnapshotPng.Read(SnapshotPng.Reframe(Encode(Gradient(6, 4)), new PhysicalRect(4, 2, 100, 100), 0));

        Assert.Equal(2, cropped.Width);
        Assert.Equal(2, cropped.Height);
    }

    [Fact]
    public void Crop_OutsideTheImage_IsRefused()
    {
        Assert.Throws<InvalidDataException>(() => SnapshotPng.Reframe(Encode(Gradient(6, 4)), new PhysicalRect(10, 10, 5, 5), 0));
    }

    [Fact]
    public void Shrink_AveragesTheCoveredPixels_AndKeepsTheAspectRatio()
    {
        // Black and white columns side by side average to grey.
        var rgba = new byte[4 * 2 * 4];
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var value = (byte)(x % 2 == 0 ? 0 : 255);
                rgba.AsSpan((((y * 4) + x) * 4), 4).Fill(value);
            }
        }

        var shrunk = SnapshotPng.Read(SnapshotPng.Reframe(Encode(new SnapshotImage(4, 2, rgba)), null, 2));

        Assert.Equal(2, shrunk.Width);
        Assert.Equal(1, shrunk.Height);
        Assert.Equal(127, shrunk.Rgba[0]);
    }

    [Fact]
    public void Shrink_LeavesSmallerImagesAlone()
    {
        var shrunk = SnapshotPng.Read(SnapshotPng.Reframe(Encode(Gradient(4, 4)), null, 100));

        Assert.Equal(4, shrunk.Width);
    }

    [Fact]
    public void ForeignBytes_AreRefused()
    {
        Assert.Throws<InvalidDataException>(() => SnapshotPng.Reframe(new byte[40], null, 10));
    }

    private static SnapshotImage Gradient(int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < rgba.Length; i++)
        {
            rgba[i] = (byte)(i * 7);
        }

        return new SnapshotImage(width, height, rgba);
    }

    private static byte[] Encode(SnapshotImage image)
    {
        using var stream = new MemoryStream();
        PngWriter.Write(stream, image);
        return stream.ToArray();
    }

    private static byte[] Pixel(SnapshotImage image, int x, int y) => image.Rgba.AsSpan(((y * image.Width) + x) * 4, 4).ToArray();
}
