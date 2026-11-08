using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Saves an offline test image for a human look (PngWriter itself only writes to a stream).</summary>
internal static class PngFile
{
    public static void Write(string path, SnapshotImage image)
    {
        using var file = File.Create(path);
        PngWriter.Write(file, image);
    }
}
