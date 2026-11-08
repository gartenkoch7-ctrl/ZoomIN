using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Copy of the drawn overlay around the pointer (M15, ADR-0026). The adaptive pointer reads what
/// is shown under it, but the render target cannot be read while it is bound, so the area under
/// the pointer is copied first. The texture grows in steps of 128 px and is reused; a frame
/// allocates nothing.
/// </summary>
internal sealed class PointerBackdrop : IDisposable
{
    // Room for the background taps around every pointer pixel (2 px) plus bilinear reach.
    private const int Margin = 4;
    private const int Step = 128;

    private readonly ID3D11Device _device;
    private ID3D11Texture2D? _texture;
    private int _width;
    private int _height;

    public PointerBackdrop(ID3D11Device device)
    {
        _device = device;
    }

    /// <summary>The copy; null before the first <see cref="Capture"/>.</summary>
    public ID3D11ShaderResourceView? View { get; private set; }

    /// <summary>Left edge of the copied area in target pixels.</summary>
    public int X { get; private set; }

    /// <summary>Top edge of the copied area in target pixels.</summary>
    public int Y { get; private set; }

    public float InverseWidth => 1f / _width;

    public float InverseHeight => 1f / _height;

    /// <summary>Copies the target area under a pointer drawn at x, y with this size.</summary>
    /// <param name="source">The render target texture (back buffer).</param>
    /// <returns>False if the area lies outside the target: nothing copied.</returns>
    public bool Capture(ID3D11DeviceContext context, ID3D11Texture2D source, int sourceWidth, int sourceHeight,
        double x, double y, double width, double height)
    {
        var left = Math.Clamp((int)Math.Floor(x) - Margin, 0, sourceWidth);
        var top = Math.Clamp((int)Math.Floor(y) - Margin, 0, sourceHeight);
        var right = Math.Clamp((int)Math.Ceiling(x + width) + Margin, left, sourceWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(y + height) + Margin, top, sourceHeight);
        (X, Y) = (left, top);
        if (right <= left || bottom <= top)
        {
            return false;
        }

        Ensure(right - left, bottom - top);
        context.CopySubresourceRegion(_texture!, 0, 0, 0, 0, source, 0, new Box(left, top, 0, right, bottom, 1));
        return true;
    }

    public void Dispose()
    {
        View?.Dispose();
        _texture?.Dispose();
    }

    // Grows only: a larger pointer (zoom) creates the texture once, later frames reuse it.
    private void Ensure(int width, int height)
    {
        if (_texture is not null && width <= _width && height <= _height)
        {
            return;
        }

        View?.Dispose();
        _texture?.Dispose();
        _width = Math.Max(_width, RoundUp(width));
        _height = Math.Max(_height, RoundUp(height));
        _texture = _device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)_width, (uint)_height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource));
        View = _device.CreateShaderResourceView(_texture);
    }

    private static int RoundUp(int value) => (value + Step - 1) / Step * Step;
}
