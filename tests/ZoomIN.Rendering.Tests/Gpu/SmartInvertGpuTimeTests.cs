using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// GPU time of the smart inversion at 1440p on the first hardware adapter (ADR-0024 budget).
/// Runs only with ZOOMIN_GPU_TIMING=1: it needs a real GPU and is a measurement, not a check.
/// </summary>
public sealed class SmartInvertGpuTimeTests
{
    private const int Width = 2560;
    private const int Height = 1440;
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
        using var ambient = new AmbientPass(gpu.Device, Width, Height);
        var monitor = new PhysicalRect(0, 0, Width, Height);
        var effects = new DisplayEffects(new FilterSettings(true, ColorFilter.InvertedSmart, 1, 0), Smoothing.Text, PointerStyle.Default);
        var windows = default(WindowConstants);
        WindowConstants.Fill(ref windows, WindowRegions.Empty, monitor, effects);
        pass.UpdateWindows(gpu.Context, windows);
        var plain = MagnifyConstants.Fullscreen(monitor, monitor, effects.Matrix, Smoothing.Text);
        var smart = MagnifyConstants.Fullscreen(monitor, monitor, effects.Matrix, Smoothing.Text, smart: true);

        var plainMs = GpuTimer.Measure(gpu, Frames, () => pass.Draw(gpu.Context, image.View, target, Width, Height, plain));
        var ambientMs = GpuTimer.Measure(gpu, Frames, () => ambient.Update(gpu.Context, image.View, 0.3f, protectPhotos: true));
        var smartMs = GpuTimer.Measure(gpu, Frames, () =>
        {
            ambient.Update(gpu.Context, image.View, 0.3f, protectPhotos: true);
            pass.Draw(gpu.Context, image.View, target, Width, Height, smart, ambient.Weights);
        });

        var report = $"1440p on {adapter.Description1.Description}: magnify {plainMs:F3} ms, ambient {ambientMs:F3} ms, "
            + $"magnify smart incl. ambient {smartMs:F3} ms, extra {smartMs - plainMs:F3} ms";
        TestContext.Current.SendDiagnosticMessage(report);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "zoomin-gpu-timing.txt"), report);
    }
}
