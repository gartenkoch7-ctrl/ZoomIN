using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// Runs the real smart inversion shaders on a captured screen image (the original, which screen
/// captures do show) and writes result and weights as PNG next to it. A tool for agents to see
/// what the overlay would draw: ZOOMIN_OFFLINE_INPUT = raw file (int width, int height, BGRA rows).
/// </summary>
public sealed class OfflineRenderTests
{
    [Fact]
    public void Render()
    {
        var input = Environment.GetEnvironmentVariable("ZOOMIN_OFFLINE_INPUT");
        Assert.SkipWhen(string.IsNullOrEmpty(input), "Set ZOOMIN_OFFLINE_INPUT to a raw BGRA file.");

        using var reader = new BinaryReader(File.OpenRead(input!));
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var bgra = reader.ReadBytes(width * height * 4);

        using var gpu = GpuDevice.CreateWarp();
        using var texture = Upload(gpu.Device, bgra, width, height);
        using var view = gpu.Device.CreateShaderResourceView(texture);
        using var targetTexture = gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)width, (uint)height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.RenderTarget));
        using var target = gpu.Device.CreateRenderTargetView(targetTexture);
        using var pass = new MagnifyPass(gpu.Device);
        using var ambient = new AmbientPass(gpu.Device, width, height);

        var monitor = new PhysicalRect(0, 0, width, height);
        var effects = new DisplayEffects(new FilterSettings(true, ColorFilter.InvertedSmart, 1, 0), Smoothing.Text, PointerStyle.Default);
        var windows = default(WindowConstants);
        WindowConstants.Fill(ref windows, WindowRegions.Empty, monitor, effects);
        pass.UpdateWindows(gpu.Context, windows);
        ambient.Update(gpu.Context, view, 1, effects.ProtectPhotos);
        pass.Draw(gpu.Context, view, target, width, height,
            MagnifyConstants.Fullscreen(monitor, monitor, effects.Matrix, Smoothing.Text, smart: true), ambient.Weights);

        var folder = Path.GetDirectoryName(input!)!;
        var name = Path.GetFileNameWithoutExtension(input!);
        using var resultResource = target.Resource;
        PngWriter.Write(Path.Combine(folder, name + "-result.png"), TextureReader.ReadBgra(gpu.Device, gpu.Context, resultResource));
        using var weightResource = ambient.Weights.Resource;
        PngWriter.Write(Path.Combine(folder, name + "-weights.png"), TextureReader.ReadWeights(gpu.Device, gpu.Context, weightResource));
    }

    private static ID3D11Texture2D Upload(ID3D11Device device, byte[] bgra, int width, int height)
    {
        var handle = GCHandle.Alloc(bgra, GCHandleType.Pinned);
        try
        {
            return device.CreateTexture2D(
                new Texture2DDescription(Format.B8G8R8A8_UNorm, (uint)width, (uint)height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource),
                new SubresourceData(handle.AddrOfPinnedObject(), (uint)(width * 4)));
        }
        finally
        {
            handle.Free();
        }
    }
}
