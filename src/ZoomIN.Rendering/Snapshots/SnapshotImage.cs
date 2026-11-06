namespace ZoomIN.Rendering.Snapshots;

/// <summary>An image read back from the GPU, 8-bit RGBA rows top-down.</summary>
internal sealed record SnapshotImage(int Width, int Height, byte[] Rgba);
