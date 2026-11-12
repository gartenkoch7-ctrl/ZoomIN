using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Tests.Gpu;
using ZoomIN.Rendering.Tests.RenderLoop;
using ZoomIN.Rendering.TextEnhance;

namespace ZoomIN.Rendering.Tests.TextEnhance;

/// <summary>
/// <c>zoomin.render.enhance_gpu_ms</c> (M18, D1): measured once per change of the values, only while
/// someone listens, and never below 1.5× or with another smoothing.
/// </summary>
[Collection(MeterTests.Name)]
public sealed class EnhanceCostTests
{
    private const string Monitor = @"\\.\DISPLAY1";
    private const int Size = 64;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly DisplayEffects s_enhanced =
        new(FilterSettings.Default, Smoothing.Text, PointerStyle.Default) { Enhancement = new TextEnhancement(0.6, 1) };

    [Fact]
    public void WithListener_MeasuresOnce_AndAgainAfterAChange()
    {
        var recorded = new List<double>();
        using var listener = Listen(recorded);
        using var setup = new Setup();

        setup.DrawUntil(1, s_enhanced);
        Assert.Equal(1, setup.Cost.Reports);
        Assert.Single(recorded);
        Assert.True(recorded[0] >= 0);

        // Same values: no new measurement. New values: one more.
        setup.DrawUntil(2, s_enhanced, frames: 400);
        Assert.Equal(1, setup.Cost.Reports);
        setup.DrawUntil(2, s_enhanced with { Enhancement = new TextEnhancement(1, 3) });
        Assert.Equal(2, setup.Cost.Reports);

        // Off for a while and back to the same values: the conditions may have changed, measure again.
        setup.DrawUntil(3, s_enhanced with { Enhancement = TextEnhancement.Off }, frames: 5);
        setup.DrawUntil(3, s_enhanced with { Enhancement = new TextEnhancement(1, 3) });
        Assert.Equal(3, setup.Cost.Reports);
    }

    [Fact]
    public void Lens_MeasuresTheLensOnly_AndLeavesTheRestUntouched()
    {
        var recorded = new List<double>();
        using var listener = Listen(recorded);
        using var setup = new Setup(4, ZoomMode.Lens);

        setup.DrawUntil(1, s_enhanced);

        // No filter: outside the lens the overlay stays transparent, probe draws included.
        Assert.Equal(1, setup.Cost.Reports);
        Assert.Equal(0, setup.Corner().A);
    }

    [Theory]
    [InlineData(false, 2.0, Smoothing.Text)]
    [InlineData(true, 1.0, Smoothing.Text)]
    [InlineData(true, 2.0, Smoothing.Soft)]
    public void NoMeasurement_WithoutListener_BelowMinZoom_OrOtherSmoothing(bool listen, double zoom, Smoothing smoothing)
    {
        var recorded = new List<double>();
        using var listener = listen ? Listen(recorded) : null;
        using var setup = new Setup(zoom);

        setup.DrawUntil(1, s_enhanced with { Smoothing = smoothing }, frames: 300);

        Assert.Equal(0, setup.Cost.Reports);
        Assert.Empty(recorded);
    }

    private static MeterListener Listen(List<double> recorded)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "zoomin.render.enhance_gpu_ms")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => recorded.Add(value));
        listener.Start();
        return listener;
    }

    private sealed class Setup : IDisposable
    {
        private readonly GpuDevice _gpu = GpuDevice.CreateWarp();
        private readonly TestImage _image;
        private readonly RenderTarget _target;
        private readonly MagnifyPass _pass;
        private readonly SmartInvertLayer _smart;
        private readonly GpuPassTimer _timer;
        private readonly PassCostProbe _probe;
        private readonly DesktopLayer _layer;
        private readonly ModeFrame _frame;

        public Setup(double zoom = 2, ZoomMode mode = ZoomMode.Fullscreen)
        {
            _image = TestImage.Create(_gpu.Device, Size);
            _target = RenderTarget.Create(_gpu.Device, Size);
            _pass = new MagnifyPass(_gpu.Device);
            _smart = SmartInvertLayer.Create(_gpu.Device, Size, Size, Monitor, new SmartInvertHealth(), NullLogger.Instance);
            _timer = new GpuPassTimer(_gpu.Device, Monitor, "magnify");
            _probe = new PassCostProbe(_gpu.Device);
            Cost = new EnhanceCost(_gpu.Device, Monitor);
            _layer = new DesktopLayer(_pass, _smart, _timer, _probe, Cost);
            _frame = ModeGeometry.Compute(mode, s_monitor, new PhysicalPoint(32, 32), zoom, LensSize.Default, DockLayout.Default);
        }

        public EnhanceCost Cost { get; }

        public Bgra Corner()
        {
            var pixels = _target.ReadBack(_gpu.Device, _gpu.Context);
            return pixels.At(0, 0);
        }

        /// <summary>Draws frames until <paramref name="stopAt"/> reports came in or 3 000 frames passed.</summary>
        public void DrawUntil(int stopAt, DisplayEffects effects, int frames = 3_000)
        {
            for (var i = 0; i < frames && Cost.Reports < stopAt; i++)
            {
                _layer.Draw(_gpu.Context, _image.View, _target.View, Size, Size, s_monitor, _frame, effects, WindowRegions.Empty, false);
                _gpu.Context.Flush();
            }
        }

        public void Dispose()
        {
            Cost.Dispose();
            _probe.Dispose();
            _timer.Dispose();
            _smart.Dispose();
            _pass.Dispose();
            _target.Dispose();
            _image.Dispose();
            _gpu.Dispose();
        }
    }
}
