using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>A captured screen image for offline renders: raw file of int width, int height, BGRA rows.</summary>
internal sealed class OfflineImage(int width, int height, byte[] bgra)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    public static OfflineImage Load(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        return new OfflineImage(width, height, reader.ReadBytes(width * height * 4));
    }

    /// <summary>Rows <paramref name="top"/> … top + height as a new image (for scrolling).</summary>
    public OfflineImage Rows(int top, int height) =>
        new(Width, height, bgra.AsSpan(top * Width * 4, height * Width * 4).ToArray());

    public ID3D11Texture2D Upload(ID3D11Device device)
    {
        var handle = GCHandle.Alloc(bgra, GCHandleType.Pinned);
        try
        {
            return device.CreateTexture2D(
                new Texture2DDescription(Format.B8G8R8A8_UNorm, (uint)Width, (uint)Height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource),
                new SubresourceData(handle.AddrOfPinnedObject(), (uint)(Width * 4)));
        }
        finally
        {
            handle.Free();
        }
    }
}
