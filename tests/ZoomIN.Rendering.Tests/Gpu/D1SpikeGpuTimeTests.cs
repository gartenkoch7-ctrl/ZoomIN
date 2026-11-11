using System.Globalization;
using System.Text;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// D1 spike (never merged): GPU time of the magnify pass without enhancement, with variant A
/// (in pass) and variant B (offscreen target + post pass) at 1080p, zoom 2×/4×/8×, with and without
/// smart inversion, sharpness 60 % and bold 1 px. Timestamp queries as in BaselineGpuTimeTests.
/// ZOOMIN_GPU_TIMING=1: first hardware adapter; ZOOMIN_GPU_TIMING=warp: WARP (relative cost only).
/// </summary>
public sealed class D1SpikeGpuTimeTests
{
    private const int Width = 1920;
    private const int Height = 1080;

    [Fact]
    public void Measure()
    {
        var mode = Environment.GetEnvironmentVariable("ZOOMIN_GPU_TIMING");
        Assert.SkipUnless(mode is "1" or "warp", "Set ZOOMIN_GPU_TIMING=1 (GPU) or warp to measure.");

        var warp = mode == "warp";
        using var gpu = warp ? GpuDevice.CreateWarp() : Hardware(out _);
        var adapterName = warp ? "WARP" : AdapterName();
        var frames = warp ? 5 : 300;
        using var image = WideImage.Create(gpu.Device, Width, Height);
        using var renderer = new D1SpikeRenderer(gpu, image.View, Width, Height, Width, Height);
        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"D1 spike GPU time, 1080p, {frames} frames, on {adapterName}");
        report.AppendLine("filter | zoom | off ms | A extra | A sharp-only extra | B extra | B copy-only extra");
        foreach (var filter in new ColorFilter?[] { null, ColorFilter.InvertedSmart })
        {
            foreach (var zoom in new[] { 2.0, 4.0, 8.0 })
            {
                // B with sharpness and bold 0 is a plain copy: the floor of any post pass.
                var copy = renderer.Prepare(filter, zoom, Width / 3, Height / 3, 0f, 0f);
                var copyOnly = GpuTimer.Measure(gpu, frames, () => renderer.Draw(D1Variant.PostPass, copy));
                var sharp = renderer.Prepare(filter, zoom, Width / 3, Height / 3, 0.6f, 0f);
                var sharpOnly = GpuTimer.Measure(gpu, frames, () => renderer.Draw(D1Variant.InPass, sharp));
                var frame = renderer.Prepare(filter, zoom, Width / 3, Height / 3, 0.6f, 1f);
                var off = GpuTimer.Measure(gpu, frames, () => renderer.Draw(D1Variant.Off, frame));
                var inPass = GpuTimer.Measure(gpu, frames, () => renderer.Draw(D1Variant.InPass, frame));
                var post = GpuTimer.Measure(gpu, frames, () => renderer.Draw(D1Variant.PostPass, frame));
                report.AppendLine(CultureInfo.InvariantCulture,
                    $"{(filter is null ? "none " : "smart")}  | {zoom}x   | {off:F3}  | {inPass - off:F3}   | {sharpOnly - off:F3}              | {post - off:F3}   | {copyOnly - off:F3}");
            }
        }

        var text = report.ToString();
        TestContext.Current.SendDiagnosticMessage(text);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), $"zoomin-d1-gpu-{(warp ? "warp" : "hw")}.txt"), text);
    }

    private static GpuDevice Hardware(out string name)
    {
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        factory.EnumAdapters1(0, out var adapter).CheckError();
        using var _ = adapter;
        name = adapter.Description1.Description;
        return GpuDevice.CreateForAdapter(adapter);
    }

    private static string AdapterName()
    {
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        factory.EnumAdapters1(0, out var adapter).CheckError();
        using var _ = adapter;
        return adapter.Description1.Description;
    }
}
