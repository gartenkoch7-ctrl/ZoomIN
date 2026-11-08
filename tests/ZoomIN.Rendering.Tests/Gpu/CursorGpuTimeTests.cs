using System.Globalization;
using System.Text;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// M15 budget: GPU time of the pointer layer, original against adaptive (backdrop copy plus eight
/// background taps per pointer pixel), on the first hardware adapter at 1440p. Runs only with
/// ZOOMIN_GPU_TIMING=1: a measurement, not a check. The table goes to the test output and
/// %TEMP%\zoomin-gpu-cursor.txt.
/// </summary>
public sealed class CursorGpuTimeTests
{
    private const int Frames = 300;
    private const int Width = 2560;
    private const int Height = 1440;

    [Fact]
    public void Measure()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("ZOOMIN_GPU_TIMING") == "1", "Set ZOOMIN_GPU_TIMING=1 to measure on the GPU.");

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        factory.EnumAdapters1(0, out var adapter).CheckError();
        using var _ = adapter;
        using var gpu = GpuDevice.CreateForAdapter(adapter);
        using var texture = gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, Width, Height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.RenderTarget));
        using var view = gpu.Device.CreateRenderTargetView(texture);
        using var captured = gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, Width, Height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource));
        using var desktop = gpu.Device.CreateShaderResourceView(captured);
        using var pass = new CursorPass(gpu.Device);
        using var backdrop = new PointerBackdrop(gpu.Device);
        var layer = new CursorLayer(pass, backdrop, new ArrowSource(), new NoSystemCursor());
        var monitor = new PhysicalRect(0, 0, Width, Height);
        var cursor = new PhysicalPoint(1200, 700);
        var target = new LayerTarget(view, texture, Width, Height, desktop);

        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"Pointer layer on {adapter.Description1.Description}, {Height}p");
        report.AppendLine("zoom | original ms | adaptive ms");
        foreach (var zoom in new[] { 1.0, 4.0 })
        {
            var frame = ModeGeometry.Compute(ZoomMode.Fullscreen, monitor, cursor, zoom, LensSize.Default, DockLayout.Default);
            double Time(PointerColorMode mode)
            {
                var effects = new DisplayEffects(FilterSettings.Default, Smoothing.Text, PointerStyle.Default with { ColorMode = mode });
                return GpuTimer.Measure(gpu, Frames, () =>
                    layer.Draw(gpu.Context, target, monitor, frame, ZoomMode.Fullscreen, zoom, cursor, effects, 0, hidden: false));
            }

            report.AppendLine(CultureInfo.InvariantCulture,
                $"{zoom:0}×   | {Time(PointerColorMode.Original):F3}       | {Time(PointerColorMode.Adaptive):F3}");
        }

        var text = report.ToString();
        TestContext.Current.SendDiagnosticMessage(text);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "zoomin-gpu-cursor.txt"), text);
    }

    private sealed class NoSystemCursor : ISystemCursor
    {
        public void SetVisible(bool visible)
        {
        }

        public void Dispose()
        {
        }
    }

    // A 32 × 32 opaque pointer, the standard size at 100 % scaling.
    private sealed class ArrowSource : ICursorShapeSource
    {
        private readonly CursorShape _shape = new(1, 32, 32, 0, 0, Enumerable.Repeat((byte)255, 32 * 32 * 4).ToArray());

        public bool TryGetCurrent(out nint handle)
        {
            handle = 1;
            return true;
        }

        public nint ArrowHandle() => 1;

        public CursorShape? Load(nint handle) => _shape;

        public CursorShape? LoadStandard(nint handle, double scale) => _shape;
    }
}
