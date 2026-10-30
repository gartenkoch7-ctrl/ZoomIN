using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ZoomIN.Rendering.Gpu;

/// <summary>A D3D11 device and its immediate context. Owned and used by one render thread.</summary>
internal sealed class GpuDevice : IDisposable
{
    private static readonly FeatureLevel[] s_featureLevels =
        [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0];

    private GpuDevice(ID3D11Device device, ID3D11DeviceContext context)
    {
        Device = device;
        Context = context;
    }

    public ID3D11Device Device { get; }

    public ID3D11DeviceContext Context { get; }

    /// <summary>Hardware device on the adapter that drives the monitor (required by Desktop Duplication).</summary>
    public static GpuDevice CreateForAdapter(IDXGIAdapter1 adapter) =>
        Create(adapter, DriverType.Unknown);

    /// <summary>Software rasterizer; used by tests and CI machines without a GPU.</summary>
    public static GpuDevice CreateWarp() => Create(null, DriverType.Warp);

    public void Dispose()
    {
        Context.ClearState();
        Context.Dispose();
        Device.Dispose();
    }

    private static GpuDevice Create(IDXGIAdapter1? adapter, DriverType driverType)
    {
        D3D11.D3D11CreateDevice(
            adapter,
            driverType,
            DeviceCreationFlags.BgraSupport,
            s_featureLevels,
            out ID3D11Device device,
            out _,
            out ID3D11DeviceContext context).CheckError();
        return new GpuDevice(device, context);
    }
}
