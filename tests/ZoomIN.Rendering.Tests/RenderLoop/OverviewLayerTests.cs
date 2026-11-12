using Vortice.Mathematics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Tests.Gpu;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>The overview frame (B2) on WARP: in the right place, amber like the lens frame, the rest transparent.</summary>
public sealed class OverviewLayerTests
{
    private const int Size = 64;
    private const int Frames = 1_000;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly Bgra s_amber = new(8, 194, 255, 255);
    private static readonly Bgra s_clear = new(0, 0, 0, 0);

    [Fact]
    public void Frame_AtOneX_MarksTheViewOfTheUsersLevel_AndLeavesTheRestTransparent()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var layer = new OverviewLayer(pass);
        var frame = OneX();
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        // Level 2 around the mouse at (32, 32): the frame is (16, 16, 32, 32), 4 px wide inside.
        layer.Update(frame, Session(), s_monitor, new PhysicalPoint(32, 32), 2, LensSize.Default, DockLayout.Default);
        layer.Draw(gpu.Context, target.View, s_monitor, frame);
        var pixels = target.ReadBack(gpu.Device, gpu.Context);

        Assert.Equal(s_amber, pixels.At(17, 30));
        Assert.Equal(s_amber, pixels.At(46, 30));
        Assert.Equal(s_amber, pixels.At(30, 17));
        Assert.Equal(s_amber, pixels.At(30, 46));
        Assert.Equal(s_clear, pixels.At(30, 30)); // inside
        Assert.Equal(s_clear, pixels.At(10, 30)); // outside
        Assert.Equal(s_clear, pixels.At(50, 50));
    }

    [Fact]
    public void Update_RecordsTheViewBeforeTheOverview_AndTracksRedraws()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var layer = new OverviewLayer(pass);
        var zoomed = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, new PhysicalPoint(8, 8), 4, LensSize.Default, DockLayout.Default);
        var session = Session();

        layer.Update(zoomed, null, s_monitor, new PhysicalPoint(8, 8), 4, LensSize.Default, DockLayout.Default);
        Assert.False(layer.NeedsRedraw);

        layer.Update(OneX(), session, s_monitor, new PhysicalPoint(9, 9), 4, LensSize.Default, DockLayout.Default);
        Assert.True(session.TryGetStart(out var view, out var mouse));
        Assert.Equal(zoomed.Source, view);
        Assert.Equal(new PhysicalPoint(9, 9), mouse);
        Assert.True(layer.NeedsRedraw);

        layer.Draw(gpu.Context, target.View, s_monitor, OneX());
        Assert.False(layer.NeedsRedraw);

        layer.Update(OneX(), session, s_monitor, new PhysicalPoint(20, 9), 4, LensSize.Default, DockLayout.Default);
        Assert.True(layer.NeedsRedraw); // the mouse moved the frame
        layer.Draw(gpu.Context, target.View, s_monitor, OneX());

        layer.Update(OneX(), null, s_monitor, new PhysicalPoint(20, 9), 4, LensSize.Default, DockLayout.Default);
        Assert.True(layer.NeedsRedraw); // the frame goes away
    }

    [Fact]
    public void Forget_AfterTheOverlayHid_TakesTheCurrentViewInstead()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var pass = new CursorPass(gpu.Device);
        var layer = new OverviewLayer(pass);
        var old = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, new PhysicalPoint(8, 8), 4, LensSize.Default, DockLayout.Default);
        layer.Update(old, null, s_monitor, default, 4, LensSize.Default, DockLayout.Default);
        var session = Session();

        layer.Forget();
        layer.Update(OneX(), session, s_monitor, new PhysicalPoint(9, 9), 4, LensSize.Default, DockLayout.Default);

        Assert.True(session.TryGetStart(out var view, out _));
        Assert.Equal(OneX().Source, view);
    }

    [Fact]
    public void UpdateAndDraw_DoNotAllocate()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var layer = new OverviewLayer(pass);
        var session = Session();
        var frame = OneX();
        void Draw(int i)
        {
            layer.Update(frame, session, s_monitor, new PhysicalPoint(20 + (i % 8), 30), 2, LensSize.Default, DockLayout.Default);
            layer.Draw(gpu.Context, target.View, s_monitor, frame);
            gpu.Context.Flush();
        }

        Draw(0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            Draw(i);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static ModeFrame OneX() =>
        ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, new PhysicalPoint(32, 32), 1, LensSize.Default, DockLayout.Default);

    private static OverviewSession Session() => new(1, @"\\.\DISPLAY1", ZoomMode.Fullscreen, 2, 0);
}
