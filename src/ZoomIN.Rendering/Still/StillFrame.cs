using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Interop;

namespace ZoomIN.Rendering.Still;

/// <summary>
/// The building block "Standbild" (M19 step 0): copies the captured desktop texture of a monitor,
/// or a part of it, into a texture of its own on demand and keeps it until released. Used by
/// frozen monitors (G2) and islands (B1) that should not follow the live image.
/// A capture of the same size reuses the texture and does not allocate; a new size or the first
/// capture creates it (on demand, not per frame). Owned by one render thread, like its device.
/// </summary>
internal sealed class StillFrame : IDisposable
{
    private static readonly UpDownCounter<long> s_held = Telemetry.Meter.CreateUpDownCounter<long>(
        "zoomin.render.still_images", unit: "{image}", description: "Still images currently held on the GPU");

    private readonly ID3D11Device _device;
    private readonly ILogger _log;
    private ID3D11Texture2D? _texture;
    private bool _disposed;

    public StillFrame(ID3D11Device device, ILogger log)
    {
        _device = device;
        _log = log;
    }

    /// <summary>The kept image; null until a capture and after <see cref="Release"/>.</summary>
    public ID3D11ShaderResourceView? ShaderResource { get; private set; }

    public bool HasImage => ShaderResource is not null;

    /// <summary>Size of the kept image in pixels.</summary>
    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>The part of the source the image shows, in source pixels.</summary>
    public PhysicalRect Source { get; private set; }

    /// <summary>Keeps the whole <paramref name="source"/> (e.g. DesktopDuplicator.Texture).</summary>
    public bool Capture(ID3D11DeviceContext context, ID3D11Texture2D source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var description = source.Description;
        return Capture(context, source, new PhysicalRect(0, 0, description.Width, description.Height));
    }

    /// <summary>
    /// Keeps the part <paramref name="region"/> (source pixels, clipped to the source and rounded
    /// outwards to whole pixels). Replaces an earlier image.
    /// </summary>
    /// <returns>False if nothing of the region lies in the source or the format is not supported.</returns>
    public bool Capture(ID3D11DeviceContext context, ID3D11Texture2D source, PhysicalRect region)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        var description = source.Description;
        if (description.Format != Format.B8G8R8A8_UNorm || description.SampleDescription.Count != 1)
        {
            _log.UnsupportedFormat(description.Format.ToString());
            return false;
        }

        var left = (uint)Math.Clamp(Math.Floor(region.X), 0, description.Width);
        var top = (uint)Math.Clamp(Math.Floor(region.Y), 0, description.Height);
        var right = (uint)Math.Clamp(Math.Ceiling(region.Right), 0, description.Width);
        var bottom = (uint)Math.Clamp(Math.Ceiling(region.Bottom), 0, description.Height);
        if (right <= left || bottom <= top)
        {
            return false;
        }

        var width = (int)(right - left);
        var height = (int)(bottom - top);
        EnsureTexture(width, height);
        // Always a region of subresource 0: unlike CopyResource it also works for a source with
        // mip levels or array slices, which CopyResource would silently skip.
        DxgiInterop.CopySubresourceRegion(context.NativePointer, _texture!.NativePointer, source.NativePointer, left, top, right, bottom);

        Source = new PhysicalRect(left, top, width, height);
        return true;
    }

    /// <summary>Frees the GPU memory (a 4K image holds about 33 MB); back to live.</summary>
    public void Release()
    {
        if (_texture is null)
        {
            return;
        }

        ShaderResource?.Dispose();
        ShaderResource = null;
        _texture.Dispose();
        _texture = null;
        Width = 0;
        Height = 0;
        Source = default;
        s_held.Add(-1);
        _log.Released();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Release();
    }

    private void EnsureTexture(int width, int height)
    {
        if (_texture is not null && width == Width && height == Height)
        {
            return;
        }

        Release();
        var texture = _device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)width, (uint)height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource));
        try
        {
            ShaderResource = _device.CreateShaderResourceView(texture);
        }
        catch
        {
            // Device removed or out of memory: hold nothing rather than half an image.
            texture.Dispose();
            throw;
        }

        _texture = texture;
        Width = width;
        Height = height;
        s_held.Add(1);
        _log.Held(width, height);
    }
}
