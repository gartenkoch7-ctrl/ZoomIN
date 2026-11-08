using System.Globalization;
using System.Text;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// M14 step 3: is copying only the changed areas (dirty rects) worth it? GPU time of the full
/// desktop copy (what <c>DesktopDuplicator</c> does per new image) against copying a part of it,
/// on the first hardware adapter. Runs only with ZOOMIN_GPU_TIMING=1: a measurement, not a check.
/// The table goes to the test output and %TEMP%\zoomin-gpu-copy.txt.
/// </summary>
public sealed class CopyGpuTimeTests
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
        report.AppendLine(CultureInfo.InvariantCulture, $"Desktop copy on {adapter.Description1.Description}");
        report.AppendLine("size  | full copy ms | 25 % area ms | 5 % area ms");
        foreach (var (width, height) in new[] { (1920, 1080), (2560, 1440), (3840, 2160) })
        {
            using var source = Texture(gpu, width, height);
            using var copy = Texture(gpu, width, height);
            var full = GpuTimer.Measure(gpu, Frames, () => gpu.Context.CopyResource(copy, source));
            var quarter = GpuTimer.Measure(gpu, Frames, () => CopyPart(gpu, copy, source, width / 2, height / 2));
            var small = GpuTimer.Measure(gpu, Frames, () => CopyPart(gpu, copy, source, width / 5, height / 4));
            report.AppendLine(CultureInfo.InvariantCulture, $"{height}p | {full:F3}        | {quarter:F3}        | {small:F3}");
        }

        var text = report.ToString();
        TestContext.Current.SendDiagnosticMessage(text);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "zoomin-gpu-copy.txt"), text);
    }

    private static ID3D11Texture2D Texture(GpuDevice gpu, int width, int height) =>
        gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)width, (uint)height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource));

    private static void CopyPart(GpuDevice gpu, ID3D11Texture2D destination, ID3D11Texture2D source, int width, int height) =>
        gpu.Context.CopySubresourceRegion(destination, 0, 0, 0, 0, source, 0, new Box(0, 0, 0, width, height, 1));
}
