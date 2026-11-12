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
/// GPU time of "Schärfe"/"Fett" (M18, D1) at 1080p on the first hardware adapter: the magnify pass
/// at 2×/4×/8×, without and with smart inversion, off against 60 %/1 px and 100 %/3 px. Runs only
/// with ZOOMIN_GPU_TIMING=1: it needs a real GPU and is a measurement (docs/kb/performance.md).
/// </summary>
public sealed class EnhanceGpuTimeTests
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
        using var ambient = new AmbientPass(gpu.Device, Width, Height);
        var monitor = new PhysicalRect(0, 0, Width, Height);

        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"D1 GPU time, 1080p, {Frames} frames, on {adapter.Description1.Description}");
        report.AppendLine("filter | zoom | off ms | extra 60 %/1 px | extra 100 %/3 px");
        foreach (var filter in new FilterId?[] { null, FilterId.InvertedSmart })
        {
            var effects = new DisplayEffects(
                new FilterSettings(filter is not null, filter ?? FilterId.Inverted, 1, 0), Smoothing.Text, PointerStyle.Default);
            var windows = default(WindowConstants);
            WindowConstants.Fill(ref windows, WindowRegions.Empty, monitor, effects);
            pass.UpdateWindows(gpu.Context, windows);
            ambient.Update(gpu.Context, image.View, 1, effects.ProtectPhotos);
            var weights = effects.IsSmart ? ambient.Weights : null;
            foreach (var zoom in new[] { 2.0, 4.0, 8.0 })
            {
                var source = new PhysicalRect(Width / 3.0, Height / 3.0, Width / zoom, Height / zoom);
                double Time(TextEnhancement enhance)
                {
                    var constants = MagnifyConstants.Fullscreen(source, monitor, effects.Matrix, Smoothing.Text, effects.IsSmart, enhance.At(zoom, Smoothing.Text));
                    return GpuTimer.Measure(gpu, Frames, () => pass.Draw(gpu.Context, image.View, target, Width, Height, constants, weights));
                }

                var off = Time(TextEnhancement.Off);
                var medium = Time(new TextEnhancement(0.6, 1)) - off;
                var full = Time(new TextEnhancement(1, 3)) - off;
                report.AppendLine(CultureInfo.InvariantCulture,
                    $"{(filter is null ? "none " : "smart")}  | {zoom}x   | {off:F3}  | {medium:F3}           | {full:F3}");
            }
        }

        var text = report.ToString();
        TestContext.Current.SendDiagnosticMessage(text);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "zoomin-gpu-timing-enhance.txt"), text);
    }
}
