using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Rendering.Gpu;

/// <summary>GPU copy of one Windows pointer image.</summary>
internal sealed class PointerTexture : IDisposable
{
    private readonly ID3D11Texture2D _texture;
    private readonly ID3D11Texture2D? _adaptiveTexture;

    private PointerTexture(ID3D11Texture2D texture, ID3D11ShaderResourceView view,
        ID3D11Texture2D? adaptiveTexture, ID3D11ShaderResourceView? adaptiveView, CursorShape shape)
    {
        _texture = texture;
        _adaptiveTexture = adaptiveTexture;
        View = view;
        AdaptiveView = adaptiveView ?? view;
        Width = shape.Width;
        Height = shape.Height;
        HotspotX = shape.HotspotX;
        HotspotY = shape.HotspotY;
        Density = shape.Density;
    }

    public ID3D11ShaderResourceView View { get; }

    /// <summary>Image for the adaptive pointer, see <see cref="CursorShape.AdaptivePixels"/>; usually <see cref="View"/>.</summary>
    public ID3D11ShaderResourceView AdaptiveView { get; }

    /// <summary>Image pixels per screen pixel at pointer size 1×, see <see cref="CursorShape.Density"/>.</summary>
    public double Density { get; }

    public int Width { get; }

    public int Height { get; }

    public int HotspotX { get; }

    public int HotspotY { get; }

    public static PointerTexture Create(ID3D11Device device, CursorShape shape)
    {
        var texture = Upload(device, shape, shape.Pixels);
        var adaptive = shape.AdaptivePixels is { } pixels ? Upload(device, shape, pixels) : null;
        return new PointerTexture(texture, device.CreateShaderResourceView(texture),
            adaptive, adaptive is null ? null : device.CreateShaderResourceView(adaptive), shape);
    }

    public void Dispose()
    {
        if (!ReferenceEquals(AdaptiveView, View))
        {
            AdaptiveView.Dispose();
        }

        View.Dispose();
        _adaptiveTexture?.Dispose();
        _texture.Dispose();
    }

    private static ID3D11Texture2D Upload(ID3D11Device device, CursorShape shape, byte[] pixels)
    {
        var description = new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)shape.Width, (uint)shape.Height, arraySize: 1, mipLevels: 1,
            bindFlags: BindFlags.ShaderResource, usage: ResourceUsage.Immutable);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            return device.CreateTexture2D(description, new SubresourceData(handle.AddrOfPinnedObject(), (uint)(shape.Width * 4)));
        }
        finally
        {
            handle.Free();
        }
    }
}
