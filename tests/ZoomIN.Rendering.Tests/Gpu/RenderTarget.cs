using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Offscreen BGRA render target that can be read back to the CPU.</summary>
internal sealed class RenderTarget : IDisposable
{
    private readonly ID3D11Texture2D _texture;
    private readonly int _size;

    private RenderTarget(ID3D11Texture2D texture, ID3D11RenderTargetView view, int size)
    {
        _texture = texture;
        View = view;
        _size = size;
    }

    public ID3D11RenderTargetView View { get; }

    public static RenderTarget Create(ID3D11Device device, int size)
    {
        var texture = device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)size, (uint)size, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.RenderTarget));
        return new RenderTarget(texture, device.CreateRenderTargetView(texture), size);
    }

    public PixelBuffer ReadBack(ID3D11Device device, ID3D11DeviceContext context)
    {
        using var staging = device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            (uint)_size,
            (uint)_size,
            arraySize: 1,
            mipLevels: 1,
            bindFlags: BindFlags.None,
            usage: ResourceUsage.Staging,
            cpuAccessFlags: CpuAccessFlags.Read));
        context.CopyResource(staging, _texture);

        var mapped = context.Map(staging, 0, MapMode.Read);
        try
        {
            var pixels = new Bgra[_size * _size];
            for (var y = 0; y < _size; y++)
            {
                var row = mapped.AsSpan<Bgra>(staging, 0, 0).Slice((int)(y * mapped.RowPitch / 4), _size);
                row.CopyTo(pixels.AsSpan(y * _size, _size));
            }

            return new PixelBuffer(pixels, _size);
        }
        finally
        {
            context.Unmap(staging, 0);
        }
    }

    public void Dispose()
    {
        View.Dispose();
        _texture.Dispose();
    }
}
