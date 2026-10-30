using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DXGI;
using Windows.Win32;
using Windows.Win32.Foundation;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Overlay;

/// <summary>
/// Flip-model swapchain shown through DirectComposition on the overlay window (ADR-0006).
/// Premultiplied alpha allows transparent areas for the lens and docked modes (M4).
/// A frame-latency waitable object with latency 1 keeps input-to-photon latency minimal.
/// </summary>
internal sealed class CompositionSwapchain : IDisposable
{
    private readonly IDXGISwapChain1 _swapChain;
    private readonly IDCompositionDevice _compositionDevice;
    private readonly IDCompositionTarget _target;
    private readonly IDCompositionVisual _visual;
    private readonly HANDLE _frameLatencyWaitable;

    public CompositionSwapchain(GpuDevice gpu, IDXGIFactory2 factory, HWND window, int width, int height)
    {
        Width = width;
        Height = height;

        var description = new SwapChainDescription1
        {
            Width = (uint)width,
            Height = (uint)height,
            Format = Format.B8G8R8A8_UNorm,
            BufferCount = 2,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = new SampleDescription(1, 0),
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Premultiplied,
            Flags = SwapChainFlags.FrameLatencyWaitableObject,
        };
        _swapChain = factory.CreateSwapChainForComposition(gpu.Device, description);

        using (var swapChain2 = _swapChain.QueryInterface<IDXGISwapChain2>())
        {
            swapChain2.MaximumFrameLatency = 1;
            _frameLatencyWaitable = new HANDLE(swapChain2.FrameLatencyWaitableObject);
        }

        using (var dxgiDevice = gpu.Device.QueryInterface<IDXGIDevice>())
        {
            _compositionDevice = DComp.DCompositionCreateDevice<IDCompositionDevice>(dxgiDevice);
        }

        _compositionDevice.CreateTargetForHwnd((IntPtr)window, true, out _target).CheckError();
        _visual = _compositionDevice.CreateVisual();
        _visual.SetContent(_swapChain);
        _target.SetRoot(_visual);
        _compositionDevice.Commit();

        // In D3D11 buffer 0 always refers to the current back buffer, even with flip model.
        using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        RenderTarget = gpu.Device.CreateRenderTargetView(backBuffer);
    }

    public ID3D11RenderTargetView RenderTarget { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Blocks until the compositor is ready for the next frame.</summary>
    /// <returns>False on timeout.</returns>
    public bool WaitForNextFrame(uint timeoutMilliseconds) =>
        PInvoke.WaitForSingleObjectEx(_frameLatencyWaitable, timeoutMilliseconds, true) == WAIT_EVENT.WAIT_OBJECT_0;

    /// <summary>Presents with VSync; throws if the device was removed.</summary>
    public void Present() => _swapChain.Present(1, PresentFlags.None).CheckError();

    public void Dispose()
    {
        RenderTarget.Dispose();
        _visual.Dispose();
        _target.Dispose();
        _compositionDevice.Dispose();
        PInvoke.CloseHandle(_frameLatencyWaitable);
        _swapChain.Dispose();
    }
}
