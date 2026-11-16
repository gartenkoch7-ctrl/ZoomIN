using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>A BGRA test texture: left half red, right half blue, top-left quarter of the left half green.</summary>
internal sealed class TestImage : IDisposable
{
    public static readonly Bgra Red = new(0, 0, 255, 255);
    public static readonly Bgra Green = new(0, 255, 0, 255);
    public static readonly Bgra Blue = new(255, 0, 0, 255);

    private readonly ID3D11Texture2D _texture;

    private TestImage(ID3D11Texture2D texture, ID3D11ShaderResourceView view)
    {
        _texture = texture;
        View = view;
    }

    public ID3D11ShaderResourceView View { get; }

    /// <summary>The texture itself, e.g. as the source of a still image (M19).</summary>
    public ID3D11Texture2D Texture => _texture;

    public static TestImage Create(ID3D11Device device, int size) =>
        Create(device, size, (x, y) => x < size / 4 && y < size / 4 ? Green : x < size / 2 ? Red : Blue);

    /// <summary>A square image with the colour <paramref name="pixel"/> returns for each x, y.</summary>
    public static TestImage Create(ID3D11Device device, int size, Func<int, int, Bgra> pixel)
    {
        var data = new Bgra[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                data[(y * size) + x] = pixel(x, y);
            }
        }

        var description = new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)size, (uint)size, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource);
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var initial = new SubresourceData(handle.AddrOfPinnedObject(), (uint)(size * 4));
            var texture = device.CreateTexture2D(description, initial);
            return new TestImage(texture, device.CreateShaderResourceView(texture));
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
