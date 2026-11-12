using Microsoft.Extensions.Logging;
using Vortice.DXGI;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Rendering.Capture;
using ZoomIN.Rendering.Freeze;
using ZoomIN.Rendering.Still;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Islands;
using ZoomIN.Rendering.Overlay;
using ZoomIN.Rendering.TextEnhance;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Builds the GPU and window resources of one monitor's pipeline (device, capture, overlay,
/// shaders, layers) and frees them in reverse order. Created on the render thread; the phases
/// are child operations of zoomin.render.pipeline_start (startup measurement, M14).
/// </summary>
internal sealed class RenderResources : IDisposable
{
    // Owned resources in creation order; disposed in reverse.
    private readonly List<IDisposable> _owned = [];

    public RenderResources(PointerServices pointer, string deviceName, SmartInvertHealth health, ILogger log)
    {
        try
        {
            IDXGIFactory2 factory;
            using (Telemetry.Source.StartActivity("zoomin.render.pipeline_start.device"))
            {
                factory = Own(DXGI.CreateDXGIFactory2<IDXGIFactory2>(false));
                Monitor = Own(MonitorOutput.Find(factory, deviceName));
                Gpu = Own(GpuDevice.CreateForAdapter(Monitor.Adapter));
            }

            using (Telemetry.Source.StartActivity("zoomin.render.pipeline_start.capture"))
            {
                Duplicator = Own(new DesktopDuplicator(Gpu, Monitor));
            }

            using (Telemetry.Source.StartActivity("zoomin.render.pipeline_start.overlay"))
            {
                Overlay = Own(new OverlayWindow(Monitor.Bounds));
                Swapchain = Own(new CompositionSwapchain(Gpu, factory, Overlay.Handle, Monitor.Width, Monitor.Height));
            }

            using (Telemetry.Source.StartActivity("zoomin.render.pipeline_start.shaders"))
            {
                var pass = Own(new MagnifyPass(Gpu.Device));
                var cursorPass = Own(new CursorPass(Gpu.Device));
                CursorLayer = new CursorLayer(cursorPass, pointer.Shapes, pointer.SystemCursor);
                Highlights = new HighlightLayer(cursorPass, new HighlightMetrics(deviceName));
                OverviewLayer = new OverviewLayer(cursorPass);
                Frozen = Own(new FrozenLayer(cursorPass, new StillFrame(Gpu.Device, log), deviceName, log));
                var smart = Own(SmartInvertLayer.Create(Gpu.Device, Monitor.Width, Monitor.Height, deviceName, health, log));
                Desktop = new DesktopLayer(
                    pass, smart, Own(new GpuPassTimer(Gpu.Device, deviceName, "magnify")), Own(new PassCostProbe(Gpu.Device)),
                    Own(new EnhanceCost(Gpu.Device, deviceName)));
                Islands = new IslandLayer(
                    pass, cursorPass, Own(new IslandStills(Gpu.Device, log)), Own(new GpuPassTimer(Gpu.Device, deviceName, "islands")));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public MonitorOutput Monitor { get; }

    public GpuDevice Gpu { get; }

    public DesktopDuplicator Duplicator { get; }

    public OverlayWindow Overlay { get; }

    public CompositionSwapchain Swapchain { get; }

    public CursorLayer CursorLayer { get; }

    public HighlightLayer Highlights { get; }

    public OverviewLayer OverviewLayer { get; }

    /// <summary>Frozen monitor (G2): kept anchor, still image and border.</summary>
    public FrozenLayer Frozen { get; }

    public DesktopLayer Desktop { get; }

    /// <summary>Islands above the zoom (M19 B1).</summary>
    public IslandLayer Islands { get; }

    public void Dispose()
    {
        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            _owned[i].Dispose();
        }

        _owned.Clear();
    }

    private T Own<T>(T resource)
        where T : IDisposable
    {
        _owned.Add(resource);
        return resource;
    }
}
