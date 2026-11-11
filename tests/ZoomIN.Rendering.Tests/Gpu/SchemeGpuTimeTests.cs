using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// GPU time of the colour replacement of own schemes (M18, C3) at 1080p on the first hardware
/// adapter: the magnify pass with a built-in filter against one with three replacements on the
/// whole monitor. Runs only with ZOOMIN_GPU_TIMING=1: it needs a real GPU and is a measurement.
/// </summary>
public sealed class SchemeGpuTimeTests
{
    private const int Width = 1920;
    private const int Height = 1080;
    private const int Frames = 300;

    [Fact]
    public void Measure()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("ZOOMIN_GPU_TIMING") == "1", "Set ZOOMIN_GPU_TIMING=1 to measure on the GPU.");

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        factory.EnumAdapters1(0, out var adapter).CheckError();
        using var _ = adapter;
        using var gpu = GpuDevice.CreateForAdapter(adapter);
        using var image = WideImage.Create(gpu.Device, Width, Height);
        using var targetTexture = gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, Width, Height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.RenderTarget));
        using var target = gpu.Device.CreateRenderTargetView(targetTexture);
        using var pass = new MagnifyPass(gpu.Device);
        var monitor = new PhysicalRect(0, 0, Width, Height);

        var builtIn = new DisplayEffects(new FilterSettings(true, FilterId.Inverted, 1, 0), Smoothing.Text, PointerStyle.Default);
        var scheme = new CustomScheme
        {
            Id = FilterId.Custom1,
            Kind = SchemeKind.Deficiency,
            Strength = 70,
            Replacements =
            [
                new ColorReplacement { From = "#FF0000", To = "#FFFF00", Tolerance = 30 },
                new ColorReplacement { From = "#00FF00", To = "#00FFFF", Tolerance = 30 },
                new ColorReplacement { From = "#0000FF", To = "#FF00FF", Tolerance = 30 },
            ],
        };
        var replacing = builtIn with { Filter = builtIn.Filter with { Filter = FilterId.Custom1 }, Schemes = [scheme] };

        var plainMs = Time(gpu, pass, image.View, target, monitor, builtIn);
        var replaceMs = Time(gpu, pass, image.View, target, monitor, replacing);

        var report = $"1080p on {adapter.Description1.Description}: magnify {plainMs:F3} ms, with 3 replacements {replaceMs:F3} ms, "
            + $"extra {replaceMs - plainMs:F3} ms";
        TestContext.Current.SendDiagnosticMessage(report);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "zoomin-gpu-timing-schemes.txt"), report);
    }

    private static double Time(
        GpuDevice gpu, MagnifyPass pass, ID3D11ShaderResourceView image, ID3D11RenderTargetView target, PhysicalRect monitor, DisplayEffects effects)
    {
        var windows = default(WindowConstants);
        WindowConstants.Fill(ref windows, WindowRegions.Empty, monitor, effects);
        pass.UpdateWindows(gpu.Context, windows);
        var constants = MagnifyConstants.Fullscreen(monitor, monitor, effects.Matrix, Smoothing.Text);
        return GpuTimer.Measure(gpu, Frames, () => pass.Draw(gpu.Context, image, target, Width, Height, constants));
    }
}
