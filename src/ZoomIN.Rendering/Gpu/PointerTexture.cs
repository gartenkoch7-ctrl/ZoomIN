using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Rendering.Gpu;

/// <summary>GPU copy of one Windows pointer image.</summary>
internal sealed class PointerTexture : IDisposable
{
    private readonly ID3D11Texture2D _texture;

    private PointerTexture(ID3D11Texture2D texture, ID3D11ShaderResourceView view, CursorShape shape)
    {
        _texture = texture;
        View = view;
        Width = shape.Width;
        Height = shape.Height;
        HotspotX = shape.HotspotX;
        HotspotY = shape.HotspotY;
    }

    public ID3D11ShaderResourceView View { get; }

    public int Width { get; }

    public int Height { get; }

    public int HotspotX { get; }

    public int HotspotY { get; }

    public static PointerTexture Create(ID3D11Device device, CursorShape shape)
    {
        var description = new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)shape.Width, (uint)shape.Height, arraySize: 1, mipLevels: 1,
            bindFlags: BindFlags.ShaderResource, usage: ResourceUsage.Immutable);
        var handle = GCHandle.Alloc(shape.Pixels, GCHandleType.Pinned);
        try
        {
            var texture = device.CreateTexture2D(description, new SubresourceData(handle.AddrOfPinnedObject(), (uint)(shape.Width * 4)));
            return new PointerTexture(texture, device.CreateShaderResourceView(texture), shape);
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
