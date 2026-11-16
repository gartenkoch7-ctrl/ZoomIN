using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Freeze;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Still;
using ZoomIN.Rendering.Tests.Gpu;
using ZoomIN.Rendering.Tests.RenderLoop;
using ZoomIN.Rendering.TextEnhance;

namespace ZoomIN.Rendering.Tests.Freeze;

/// <summary>A FrozenLayer on WARP with a 64x64 source texture and a desktop layer to draw through (G2 tests).</summary>
internal sealed class FrozenLayerRig : IDisposable
{
    public const int Size = 64;
    public const uint Red = 0xFFFF0000u;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private readonly CursorPass _cursor;
    private readonly MagnifyPass _magnify;
    private readonly SmartInvertLayer _smart;
    private readonly GpuPassTimer _timer;
    private readonly PassCostProbe _probe;
    private readonly EnhanceCost _enhance;
    private readonly DesktopLayer _desktop;

    public FrozenLayerRig()
    {
        Gpu = GpuDevice.CreateWarp();
        Source = Gpu.Device.CreateTexture2D(
            new Texture2DDescription(Format.B8G8R8A8_UNorm, Size, Size, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource));
        View = Gpu.Device.CreateShaderResourceView(Source);
        Fill(Red);
        Target = RenderTarget.Create(Gpu.Device, Size);
        _cursor = new CursorPass(Gpu.Device);
        _magnify = new MagnifyPass(Gpu.Device);
        _smart = SmartInvertLayer.Create(Gpu.Device, Size, Size, @"\\.\DISPLAY1", new SmartInvertHealth(), NullLogger.Instance);
        _timer = new GpuPassTimer(Gpu.Device, @"\\.\DISPLAY1", "magnify");
        _probe = new PassCostProbe(Gpu.Device);
        _enhance = new EnhanceCost(Gpu.Device, @"\\.\DISPLAY1");
        _desktop = new DesktopLayer(_magnify, _smart, _timer, _probe, _enhance);
        Layer = new FrozenLayer(_cursor, new StillFrame(Gpu.Device, NullLogger.Instance), @"\\.\DISPLAY1", NullLogger.Instance);
    }

    public GpuDevice Gpu { get; }

    public ID3D11Texture2D Source { get; }

    public ID3D11ShaderResourceView View { get; }

    public RenderTarget Target { get; }

    public FrozenLayer Layer { get; }

    public void Fill(uint color)
    {
        var data = Enumerable.Repeat(color, Size * Size).ToArray();
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            Gpu.Context.UpdateSubresource(Source, 0, null, handle.AddrOfPinnedObject(), Size * 4, 0);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Draws the frozen view at 2× around <paramref name="anchor"/>; returns the middle pixel as 0xAARRGGBB.</summary>
    public uint Draw(PhysicalPoint anchor)
    {
        var frame = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, anchor, 2, LensSize.Default, DockLayout.Default);
        _desktop.Draw(Gpu.Context, Layer.Desktop(View), Target.View, Size, Size, s_monitor, frame, DisplayEffects.Default, WindowRegions.Empty, false);
        var pixel = Target.ReadBack(Gpu.Device, Gpu.Context).At(Size / 2, Size / 2);
        return ((uint)pixel.A << 24) | ((uint)pixel.R << 16) | ((uint)pixel.G << 8) | pixel.B;
    }

    public void Dispose()
    {
        Layer.Dispose();
        _enhance.Dispose();
        _probe.Dispose();
        _timer.Dispose();
        _smart.Dispose();
        _magnify.Dispose();
        _cursor.Dispose();
        Target.Dispose();
        View.Dispose();
        Source.Dispose();
        Gpu.Dispose();
    }
}
