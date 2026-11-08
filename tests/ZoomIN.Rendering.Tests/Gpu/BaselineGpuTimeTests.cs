using System.Globalization;
using System.Text;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// Baseline of M14 step 1: GPU time of the magnify pass at 1080p and 1440p, zoom 1×/2×/4×, with
/// and without smart inversion, on the first hardware adapter. Runs only with ZOOMIN_GPU_TIMING=1:
/// a measurement, not a check. The table goes to the test output and %TEMP%\zoomin-gpu-baseline.txt.
/// </summary>
public sealed class BaselineGpuTimeTests
{
    private const int Frames = 200;

    [Fact]
    public void Measure()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("ZOOMIN_GPU_TIMING") == "1", "Set ZOOMIN_GPU_TIMING=1 to measure on the GPU.");

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        factory.EnumAdapters1(0, out var adapter).CheckError();
        using var _ = adapter;
        using var gpu = GpuDevice.CreateForAdapter(adapter);
        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"GPU baseline on {adapter.Description1.Description}");
        report.AppendLine("size  | zoom | magnify ms | ambient ms | magnify smart ms | smart extra ms");
        foreach (var (width, height) in new[] { (1920, 1080), (2560, 1440) })
        {
            foreach (var zoom in new[] { 1.0, 2.0, 4.0 })
            {
                var row = MeasureOne(gpu, width, height, zoom);
                report.AppendLine(CultureInfo.InvariantCulture,
                    $"{height}p | {zoom}x   | {row.Plain:F3}      | {row.Ambient:F3}      | {row.Smart:F3}            | {row.Smart + row.Ambient - row.Plain:F3}");
            }
        }

        var text = report.ToString();
        TestContext.Current.SendDiagnosticMessage(text);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "zoomin-gpu-baseline.txt"), text);
    }

    private static (double Plain, double Ambient, double Smart) MeasureOne(GpuDevice gpu, int width, int height, double zoom)
    {
        using var image = WideImage.Create(gpu.Device, width, height);
        using var targetTexture = gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)width, (uint)height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.RenderTarget));
        using var target = gpu.Device.CreateRenderTargetView(targetTexture);
        using var pass = new MagnifyPass(gpu.Device);
        using var ambient = new AmbientPass(gpu.Device, width, height);
        var monitor = new PhysicalRect(0, 0, width, height);
        var source = ViewportCalculator.SourceRect(monitor, new PhysicalPoint(width / 3, height / 3), zoom);
        var effects = new DisplayEffects(new FilterSettings(true, ColorFilter.InvertedSmart, 1, 0), Smoothing.Text, PointerStyle.Default);
        var windows = default(WindowConstants);
        WindowConstants.Fill(ref windows, WindowRegions.Empty, monitor, effects);
        pass.UpdateWindows(gpu.Context, windows);
        var plain = MagnifyConstants.Fullscreen(source, monitor, effects.Matrix, Smoothing.Text);
        var smart = MagnifyConstants.Fullscreen(source, monitor, effects.Matrix, Smoothing.Text, smart: true);
        ambient.Update(gpu.Context, image.View, 1f, protectPhotos: true);

        var plainMs = GpuTimer.Measure(gpu, Frames, () => pass.Draw(gpu.Context, image.View, target, width, height, plain));
        var ambientMs = GpuTimer.Measure(gpu, Frames, () => ambient.Update(gpu.Context, image.View, 0.3f, protectPhotos: true));
        var smartMs = GpuTimer.Measure(gpu, Frames, () => pass.Draw(gpu.Context, image.View, target, width, height, smart, ambient.Weights));
        return (plainMs, ambientMs, smartMs);
    }
}
