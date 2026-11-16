using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Islands;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Tests.Gpu;

namespace ZoomIN.Rendering.Tests.Islands;

/// <summary>
/// GPU time of four islands (M19 B1) at 1080p on the first hardware adapter: the fullscreen magnify
/// pass at 4× alone against the same plus four islands (300×80 source at 2×, two live, two still),
/// without filter, with smart inversion and with "Schärfe"/"Fett". Runs only with
/// ZOOMIN_GPU_TIMING=1: it needs a real GPU and is a measurement (docs/kb/performance.md).
/// </summary>
public sealed class IslandGpuTimeTests
{
    private const int Width = 1920;
    private const int Height = 1080;
    private const int Frames = 300;
    private const string Monitor = @"\\.\TIMING";

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
        using var marks = new CursorPass(gpu.Device);
        using var ambient = new AmbientPass(gpu.Device, Width, Height);
        using var stills = new IslandStills(gpu.Device, NullLogger.Instance);
        using var time = new GpuPassTimer(gpu.Device, Monitor, "islands");
        var layer = new IslandLayer(pass, marks, stills, time);
        var monitor = new PhysicalRect(0, 0, Width, Height);
        var scene = Scene();

        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"B1 GPU time, 1080p at 4x, {Frames} frames, on {adapter.Description1.Description}");
        report.AppendLine("case | main ms | main + 4 islands ms | extra ms");
        foreach (var (name, filter, enhance) in new (string, FilterId?, TextEnhancement)[]
        {
            ("plain", null, TextEnhancement.Off),
            ("smart", FilterId.InvertedSmart, TextEnhancement.Off),
            ("sharp+bold", null, new TextEnhancement(0.6, 1)),
        })
        {
            var effects = new DisplayEffects(new FilterSettings(filter is not null, filter ?? FilterId.Inverted, 1, 0), Smoothing.Text, PointerStyle.Default)
            {
                Enhancement = enhance,
            };
            var windows = default(WindowConstants);
            WindowConstants.Fill(ref windows, WindowRegions.Empty, monitor, effects);
            pass.UpdateWindows(gpu.Context, windows);
            ambient.Update(gpu.Context, image.View, 1, effects.ProtectPhotos);
            var weights = effects.IsSmart ? ambient.Weights : null;
            var source = new PhysicalRect(Width / 3.0, Height / 3.0, Width / 4.0, Height / 4.0);
            var main = MagnifyConstants.Fullscreen(source, monitor, effects.Matrix, Smoothing.Text, effects.IsSmart, enhance.At(4, Smoothing.Text));
            void Main() => pass.Draw(gpu.Context, image.View, target, Width, Height, main, weights);

            var alone = GpuTimer.Measure(gpu, Frames, Main);
            var with = GpuTimer.Measure(gpu, Frames, () =>
            {
                Main();
                layer.Draw(gpu.Context, image.View, image.Texture, target, Monitor, monitor, scene, effects, weights);
            });
            report.AppendLine(CultureInfo.InvariantCulture, $"{name,-10} | {alone:F3}   | {with:F3}               | {with - alone:F3}");
        }

        var text = report.ToString();
        TestContext.Current.SendDiagnosticMessage(text);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "zoomin-gpu-timing-islands.txt"), text);
    }

    private static IslandScene Scene()
    {
        var set = IslandSet.Empty;
        for (var i = 0; i < IslandRules.MaxCount; i++)
        {
            set = set.Add(new Island(0, Monitor, new PhysicalRect(200 + (i * 400), 950, 300, 80), new PhysicalPoint(i * 440, 20 + (i % 2 * 300)), 2, Still: i >= 2))!;
        }

        return new IslandScene(set, true, null);
    }
}
