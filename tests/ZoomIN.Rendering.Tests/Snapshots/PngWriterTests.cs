using System.Buffers.Binary;
using System.IO.Compression;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.Tests.Snapshots;

public sealed class PngWriterTests
{
    [Fact]
    public void WritesAValidPng_WithThePixelsInIdat()
    {
        var rgba = new byte[2 * 2 * 4];
        for (var i = 0; i < rgba.Length; i++)
        {
            rgba[i] = (byte)(i * 10);
        }

        var path = Path.Combine(Path.GetTempPath(), $"zoomin-png-{Guid.NewGuid():N}.png");
        try
        {
            PngWriter.Write(path, new SnapshotImage(2, 2, rgba));
            var bytes = File.ReadAllBytes(path);

            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
            Assert.Equal("IHDR"u8.ToArray(), bytes[12..16]);
            Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)));
            Assert.Equal(new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 }, bytes[^8..]); // IEND + its fixed CRC

            var idat = 8 + 25;
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(idat));
            using var zlib = new ZLibStream(new MemoryStream(bytes, idat + 8, length), CompressionMode.Decompress);
            using var raw = new MemoryStream();
            zlib.CopyTo(raw);
            var rows = raw.ToArray();
            Assert.Equal(0, rows[0]);                       // filter byte of row 0
            Assert.Equal(rgba[..8], rows[1..9]);            // row 0
            Assert.Equal(rgba[8..], rows[10..18]);          // row 1 after its filter byte
        }
        finally
        {
            File.Delete(path);
        }
    }
}
