using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>D1 spike variants: without enhancement, A = inside the magnify pass, B = own post pass.</summary>
internal enum D1Variant
{
    Off,
    InPass,
    PostPass,
}

/// <summary>
/// D1 spike (never merged): magnifies a desktop texture into an output target with one of the
/// <see cref="D1Variant"/>s, optionally under a colour filter (also the smart inversion).
/// </summary>
internal sealed class D1SpikeRenderer : IDisposable
{
    private readonly GpuDevice _gpu;
    private readonly ID3D11ShaderResourceView _desktop;
    private readonly ID3D11Texture2D _target;
    private readonly MagnifyPass _plain;
    private readonly MagnifyPass _enhanced;
    private readonly EnhancePass _post;
    private readonly AmbientPass _ambient;
    private readonly PhysicalRect _monitor;

    public D1SpikeRenderer(GpuDevice gpu, ID3D11ShaderResourceView desktop, int desktopWidth, int desktopHeight, int width, int height)
    {
        _gpu = gpu;
        _desktop = desktop;
        OutputWidth = width;
        OutputHeight = height;
        _monitor = new PhysicalRect(0, 0, desktopWidth, desktopHeight);
        _target = gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)width, (uint)height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.RenderTarget));
        Target = gpu.Device.CreateRenderTargetView(_target);
        _plain = new MagnifyPass(gpu.Device);
        _enhanced = new MagnifyPass(gpu.Device, enhanced: true);
        _post = new EnhancePass(gpu.Device, width, height);
        _ambient = new AmbientPass(gpu.Device, desktopWidth, desktopHeight);
    }

    public int OutputWidth { get; }

    public int OutputHeight { get; }

    public ID3D11RenderTargetView Target { get; }

    public ID3D11Texture2D TargetTexture => _target;

    public static DisplayEffects Effects(ColorFilter? filter) =>
        new(new FilterSettings(filter is not null, filter ?? ColorFilter.Inverted, 1, 0), Smoothing.Text, PointerStyle.Default);

    /// <summary>Sets up filter, zoom and enhancement; returns what <see cref="Draw"/> needs per frame.</summary>
    public D1Frame Prepare(ColorFilter? filter, double zoom, int centreX, int centreY, float sharpness, float boldPx)
    {
        var effects = Effects(filter);
        var windows = default(WindowConstants);
        WindowConstants.Fill(ref windows, WindowRegions.Empty, _monitor, effects);
        _plain.UpdateWindows(_gpu.Context, windows);
        _enhanced.UpdateWindows(_gpu.Context, windows);
        var w = OutputWidth / zoom;
        var h = OutputHeight / zoom;
        var x = Math.Clamp(centreX - (w / 2), 0, _monitor.Width - w);
        var y = Math.Clamp(centreY - (h / 2), 0, _monitor.Height - h);
        var source = new PhysicalRect(x, y, w, h);
        var enhance = new EnhanceConstants(sharpness, boldPx, zoom, OutputWidth, OutputHeight);
        _enhanced.UpdateEnhance(_gpu.Context, enhance);
        if (effects.IsSmart)
        {
            _ambient.Update(_gpu.Context, _desktop, 1f, effects.ProtectPhotos);
        }

        return new D1Frame(MagnifyConstants.Fullscreen(source, _monitor, effects.Matrix, Smoothing.Text, effects.IsSmart), enhance, effects.IsSmart);
    }

    /// <summary>One frame of the magnify (and enhance) passes; the GPU work that is timed.</summary>
    public void Draw(D1Variant variant, in D1Frame frame)
    {
        var weights = frame.Smart ? _ambient.Weights : null;
        switch (variant)
        {
            case D1Variant.Off:
                _plain.Draw(_gpu.Context, _desktop, Target, OutputWidth, OutputHeight, frame.Magnify, weights);
                break;
            case D1Variant.InPass:
                _enhanced.Draw(_gpu.Context, _desktop, Target, OutputWidth, OutputHeight, frame.Magnify, weights);
                break;
            default:
                _plain.Draw(_gpu.Context, _desktop, _post.Target, OutputWidth, OutputHeight, frame.Magnify, weights);
                _post.Draw(_gpu.Context, Target, frame.Enhance);
                break;
        }
    }

    public void Dispose()
    {
        _ambient.Dispose();
        _post.Dispose();
        _enhanced.Dispose();
        _plain.Dispose();
        Target.Dispose();
        _target.Dispose();
    }
}

/// <summary>Constants of one spike frame.</summary>
internal readonly record struct D1Frame(MagnifyConstants Magnify, EnhanceConstants Enhance, bool Smart);
