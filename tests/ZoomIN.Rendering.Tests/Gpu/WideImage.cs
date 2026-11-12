using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>A desktop-sized BGRA texture with mixed content (bars, text lines, colour patches) for timing.</summary>
internal sealed class WideImage : IDisposable
{
    private readonly ID3D11Texture2D _texture;

    private WideImage(ID3D11Texture2D texture, ID3D11ShaderResourceView view)
    {
        _texture = texture;
        View = view;
    }

    public ID3D11ShaderResourceView View { get; }

    /// <summary>The texture itself, e.g. as the source of a still image (M19).</summary>
    public ID3D11Texture2D Texture => _texture;

    public static WideImage Create(ID3D11Device device, int width, int height)
    {
        var data = new Bgra[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                data[(y * width) + x] = y < 120 ? new Bgra(32, 32, 32, 255)
                    : x > width * 2 / 3 ? new Bgra((byte)(x * 7), (byte)(y * 5), (byte)((x + y) * 3), 255)
                    : y % 18 < 3 && x % 9 < 6 ? new Bgra(0, 0, 0, 255) : new Bgra(255, 255, 255, 255);
            }
        }

        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var texture = device.CreateTexture2D(
                new Texture2DDescription(Format.B8G8R8A8_UNorm, (uint)width, (uint)height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource),
                new SubresourceData(handle.AddrOfPinnedObject(), (uint)(width * 4)));
            return new WideImage(texture, device.CreateShaderResourceView(texture));
        }
        finally
        {
            handle.Free();
        }
    }

    public void Dispose()
    {
        View.Dispose();
        _texture.Dispose();
    }
}
