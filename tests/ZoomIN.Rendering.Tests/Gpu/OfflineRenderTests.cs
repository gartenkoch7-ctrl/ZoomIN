using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// Runs the real smart inversion shaders on a captured screen image (the original, which screen
/// captures do show) and writes result and weights as PNG next to it – a tool for agents to see
/// what the overlay would draw. ZOOMIN_OFFLINE_INPUT = raw file (see <see cref="OfflineImage"/>);
/// optional ZOOMIN_OFFLINE_PROTECT=0 (photos not protected), ZOOMIN_OFFLINE_ZOOM=factor.
/// </summary>
public sealed class OfflineRenderTests
{
    [Fact]
    public void Render()
    {
        var input = Environment.GetEnvironmentVariable("ZOOMIN_OFFLINE_INPUT");
        Assert.SkipWhen(string.IsNullOrEmpty(input), "Set ZOOMIN_OFFLINE_INPUT to a raw BGRA file.");
        var protect = Environment.GetEnvironmentVariable("ZOOMIN_OFFLINE_PROTECT") != "0";
        var zoom = double.TryParse(Environment.GetEnvironmentVariable("ZOOMIN_OFFLINE_ZOOM"), System.Globalization.CultureInfo.InvariantCulture, out var z) ? z : 1;

        var image = OfflineImage.Load(input!);
        using var gpu = GpuDevice.CreateWarp();
        using var renderer = new OfflineRenderer(gpu, image.Width, image.Height);
        using var texture = image.Upload(gpu.Device);
        using var view = gpu.Device.CreateShaderResourceView(texture);
        var monitor = new PhysicalRect(0, 0, image.Width, image.Height);
        var source = new PhysicalRect(
            (image.Width - (image.Width / zoom)) / 2, (image.Height - (image.Height / zoom)) / 2, image.Width / zoom, image.Height / zoom);
        renderer.Draw(view, monitor, source, protect, alpha: 1);

        var suffix = (protect ? string.Empty : "-unprotected") + (zoom != 1 ? $"-zoom{zoom:0.#}" : string.Empty);
        var stem = Path.Combine(Path.GetDirectoryName(input!)!, Path.GetFileNameWithoutExtension(input!) + suffix);
        PngWriter.Write(stem + "-result.png", renderer.ReadResult());
        PngWriter.Write(stem + "-weights.png", renderer.ReadWeights());
        PngWriter.Write(stem + "-decisions.png", renderer.ReadDecisions());
    }
}

/// <summary>The smart inversion pipeline on WARP at a given size, for offline renders.</summary>
internal sealed class OfflineRenderer : IDisposable
{
    private readonly GpuDevice _gpu;
    private readonly ID3D11Texture2D _target;
    private readonly ID3D11RenderTargetView _targetView;
    private readonly MagnifyPass _pass;
    private readonly AmbientPass _ambient;
    private readonly int _width;
    private readonly int _height;

    public OfflineRenderer(GpuDevice gpu, int width, int height)
    {
        _gpu = gpu;
        _width = width;
        _height = height;
        _target = gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)width, (uint)height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.RenderTarget));
        _targetView = gpu.Device.CreateRenderTargetView(_target);
        _pass = new MagnifyPass(gpu.Device);
        _ambient = new AmbientPass(gpu.Device, width, height);
    }

    /// <summary>One frame: weights blended with <paramref name="alpha"/>, then the monitor smart-filtered.</summary>
    public void Draw(ID3D11ShaderResourceView desktop, PhysicalRect monitor, PhysicalRect source, bool protectPhotos, float alpha)
    {
        var effects = new DisplayEffects(new FilterSettings(true, ColorFilter.InvertedSmart, 1, 0), Smoothing.Text, PointerStyle.Default)
        {
            ProtectPhotos = protectPhotos,
        };
        var windows = default(WindowConstants);
        WindowConstants.Fill(ref windows, WindowRegions.Empty, monitor, effects);
        _pass.UpdateWindows(_gpu.Context, windows);
        _ambient.Update(_gpu.Context, desktop, alpha, protectPhotos);
        _pass.Draw(_gpu.Context, desktop, _targetView, _width, _height,
            MagnifyConstants.Fullscreen(source, monitor, effects.Matrix, Smoothing.Text, smart: true), _ambient.Weights);
    }

    public SnapshotImage ReadResult() => TextureReader.ReadBgra(_gpu.Device, _gpu.Context, _target);

    public SnapshotImage ReadWeights()
    {
        using var resource = _ambient.Weights.Resource;
        return TextureReader.ReadWeights(_gpu.Device, _gpu.Context, resource);
    }

    /// <summary>Step 1 per texel: red = paper, green = photo, blue = light without paper.</summary>
    public SnapshotImage ReadDecisions()
    {
        using var resource = _ambient.RawDecisions.Resource;
        using var texture = resource.QueryInterface<ID3D11Texture2D>();
        var description = texture.Description;
        using var staging = _gpu.Device.CreateTexture2D(new Texture2DDescription(
            description.Format, description.Width, description.Height, 1, 1, BindFlags.None, ResourceUsage.Staging, CpuAccessFlags.Read));
        _gpu.Context.CopyResource(staging, texture);
        var mapped = _gpu.Context.Map(staging, 0, MapMode.Read);
        var w = (int)description.Width;
        var h = (int)description.Height;
        var rgba = new byte[w * h * 4];
        var all = mapped.AsSpan<byte>(staging, 0, 0);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var offset = (int)(y * mapped.RowPitch) + (x * 8);
                var at = ((y * w) + x) * 4;
                for (var (channel, target) = (0, 0); target < 3; target++)
                {
                    channel = target switch { 0 => 0, 1 => 2, _ => 3 };
                    rgba[at + target] = (float)BitConverter.ToHalf(all.Slice(offset + (channel * 2), 2)) > 0.5f ? (byte)255 : (byte)0;
                }

                rgba[at + 3] = 255;
            }
        }

        _gpu.Context.Unmap(staging, 0);
        return new SnapshotImage(w, h, rgba);
    }

    public void Dispose()
    {
        _ambient.Dispose();
        _pass.Dispose();
        _targetView.Dispose();
        _target.Dispose();
    }
}
