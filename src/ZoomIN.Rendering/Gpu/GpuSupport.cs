using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Checks whether this machine can run the GPU zoom pipeline at all.</summary>
public static class GpuSupport
{
    /// <summary>
    /// Feature level 10.0 is enough for our single-pass shaders and is available on every
    /// GPU that supports Windows 10 Desktop Duplication.
    /// </summary>
    public static FeatureLevel MinimumFeatureLevel => FeatureLevel.Level_10_0;

    public static bool IsHardwareDeviceAvailable()
    {
        var result = D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            [MinimumFeatureLevel],
            out ID3D11Device? device);
        device?.Dispose();
        return result.Success;
    }
}
