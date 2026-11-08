using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Tests.Gpu;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>
/// The layers RenderFrame draws every frame (desktop, highlights, pointer) must not allocate once
/// warmed up, with GPU timing on (AGENTS.md 2.3, M14 step 1).
/// </summary>
[Collection(MeterTests.Name)]
public sealed class RenderLayersAllocationTests
{
    private const int Size = 64;
    private const int Frames = 1_000;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DesktopLayer_FullscreenAndLens_DoNotAllocate(bool lens)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        using var smart = SmartInvertLayer.Create(gpu.Device, Size, Size, @"\\.\DISPLAY1", new SmartInvertHealth(), NullLogger.Instance);
        using var timer = new GpuPassTimer(gpu.Device, @"\\.\DISPLAY1", "magnify");
        using var probe = new SmartCostProbe(gpu.Device);
        var layer = new DesktopLayer(pass, smart, timer, probe);
        var effects = new DisplayEffects(new FilterSettings(true, ColorFilter.InvertedSmart, 1, 0), Smoothing.Text, PointerStyle.Default);
        var windows = new WindowRegions([new PhysicalRect(0, 0, 32, 32)], [WindowPalette.Smart]);
        var frame = lens
            ? ModeGeometry.Compute(ZoomMode.Lens, s_monitor, new PhysicalPoint(20, 20), 2, LensSize.Default, DockLayout.Default)
            : ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, new PhysicalPoint(20, 20), 2, LensSize.Default, DockLayout.Default);

        // GPU timing runs only while someone listens (as with diagnostics or OTLP on).
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "zoomin.render.gpu_time")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.Start();

        // Flush stands in for Present, which hands the frame and its queries to the GPU.
        void Draw()
        {
            layer.Draw(gpu.Context, image.View, target.View, Size, Size, s_monitor, frame, effects, windows, true);
            gpu.Context.Flush();
        }

        Draw();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            Draw();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(timer.Samples > 0, "The magnify pass was timed.");
    }

    [Fact]
    public void HighlightLayer_FocusAndCaret_DoNotAllocate()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var layer = new HighlightLayer(pass);
        var frame = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, new PhysicalPoint(32, 32), 2, LensSize.Default, DockLayout.Default);
        var tracking = new TrackingSnapshot(new PhysicalRect(20, 20, 20, 10), 1, new PhysicalRect(30, 22, 1, 8), 1);

        layer.Draw(gpu.Context, target.View, s_monitor, frame, tracking, TrackingOptions.Default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            layer.Draw(gpu.Context, target.View, s_monitor, frame, tracking, TrackingOptions.Default);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CursorLayer_PointerCrosshairAndLocator_DoNotAllocate(bool overrideWindows)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var systemCursor = new CountingSystemCursor();
        var layer = new CursorLayer(pass, new OneShapeSource(), systemCursor);
        var style = PointerStyle.Default with { OverrideWindows = overrideWindows, CrosshairEnabled = true, CrosshairAlways = true };
        var effects = new DisplayEffects(FilterSettings.Default, Smoothing.Text, style);
        var frame = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, new PhysicalPoint(32, 32), 2, LensSize.Default, DockLayout.Default);

        // The locator was just requested, so the ring animates in every measured frame.
        var locate = Stopwatch.GetTimestamp();
        bool Draw(int i) => layer.Draw(gpu.Context, target.View, s_monitor, frame, ZoomMode.Fullscreen, 2,
            new PhysicalPoint(30 + (i % 4), 32), effects, locate, hidden: false);

        // Warm-up: JIT and every pointer position of the loop once.
        for (var i = 0; i < 4; i++)
        {
            Draw(i);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var animating = 0;
        for (var i = 0; i < Frames; i++)
        {
            animating += Draw(i) ? 1 : 0;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(Frames, animating);
        Assert.Equal(Frames + 4, systemCursor.Calls);
    }

    private sealed class CountingSystemCursor : ISystemCursor
    {
        public int Calls { get; private set; }

        public void SetVisible(bool visible) => Calls++;

        public void Dispose()
        {
        }
    }

    private sealed class OneShapeSource : ICursorShapeSource
    {
        private const int Side = 16;
        private readonly CursorShape _shape = new(1, Side, Side, 0, 0, Enumerable.Repeat((byte)255, Side * Side * 4).ToArray());

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
