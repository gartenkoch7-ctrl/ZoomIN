using System.Buffers.Binary;
using System.IO.Compression;

namespace ZoomIN.Rendering.Snapshots;

/// <summary>
/// Minimal PNG encoder (8-bit RGBA, no filter) for debug snapshots, so Rendering needs no imaging
/// library: signature, IHDR, one zlib IDAT, IEND, each with its CRC-32.
/// </summary>
internal static class PngWriter
{
    private static readonly byte[] s_signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] s_crcTable = BuildCrcTable();

    /// <summary>Writes the image as PNG into <paramref name="file"/>; the caller owns the stream (no IO here, ADR-0025).</summary>
    public static void Write(Stream file, SnapshotImage image)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(image);
        file.Write(s_signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], image.Height);
        header[8] = 8;  // bits per channel
        header[9] = 6;  // RGBA
        header[10] = 0; // deflate
        header[11] = 0; // standard filters
        header[12] = 0; // no interlace
        WriteChunk(file, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var stride = image.Width * 4;
            for (var y = 0; y < image.Height; y++)
            {
                zlib.WriteByte(0); // filter "none" for this row
                zlib.Write(image.Rgba, y * stride, stride);
            }
        }

        WriteChunk(file, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(file, "IEND"u8, []);
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        stream.Write(number);
        stream.Write(type);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc(Crc(0xFFFFFFFFu, type), data) ^ 0xFFFFFFFFu);
        stream.Write(number);
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            crc = s_crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
