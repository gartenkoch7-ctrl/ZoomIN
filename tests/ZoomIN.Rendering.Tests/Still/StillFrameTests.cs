using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Still;

namespace ZoomIN.Rendering.Tests.Still;

/// <summary>The building block "Standbild" on WARP: keeps a copy while the source changes.</summary>
public sealed class StillFrameTests
{
    private const int Size = 64;

    [Fact]
    public void Capture_KeepsTheImage_WhenTheSourceChanges()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var source = CreateSource(gpu.Device, (x, _) => x < Size / 2 ? 0xFF0000FFu : 0xFFFF0000u);
        using var still = new StillFrame(gpu.Device, NullLogger.Instance);

        Assert.True(still.Capture(gpu.Context, source));
        Fill(gpu.Context, source, 0xFF00FF00u);

        Assert.True(still.HasImage);
        Assert.Equal((Size, Size), (still.Width, still.Height));
        var pixels = Read(gpu, still);
        Assert.Equal(0xFF0000FFu, pixels[(10 * Size) + 5]);
        Assert.Equal(0xFFFF0000u, pixels[(10 * Size) + 50]);
    }

    [Fact]
    public void Capture_Region_CopiesOnlyThatPart()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var source = CreateSource(gpu.Device, (x, y) => (uint)(0xFF000000 | (uint)(x << 8) | (uint)y));
        using var still = new StillFrame(gpu.Device, NullLogger.Instance);

        Assert.True(still.Capture(gpu.Context, source, new PhysicalRect(16.5, 8, 10, 4)));

        Assert.Equal((11, 4), (still.Width, still.Height));
        Assert.Equal(new PhysicalRect(16, 8, 11, 4), still.Source);
        var pixels = Read(gpu, still);
        Assert.Equal(0xFF000000u | (16u << 8) | 8u, pixels[0]);
        Assert.Equal(0xFF000000u | (26u << 8) | 11u, pixels[(3 * 11) + 10]);
    }

    [Fact]
    public void Capture_RegionOutsideTheSource_KeepsNothing()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var source = CreateSource(gpu.Device, (_, _) => 0xFFFFFFFFu);
        using var still = new StillFrame(gpu.Device, NullLogger.Instance);

        Assert.False(still.Capture(gpu.Context, source, new PhysicalRect(Size, 0, 10, 10)));
        Assert.False(still.HasImage);
    }

    [Fact]
    public void Release_FreesTheImage_AndAllowsANewCapture()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var source = CreateSource(gpu.Device, (_, _) => 0xFF123456u);
        var still = new StillFrame(gpu.Device, NullLogger.Instance);
        still.Capture(gpu.Context, source);

        still.Release();
        Assert.False(still.HasImage);
        Assert.Null(still.ShaderResource);

        Assert.True(still.Capture(gpu.Context, source, new PhysicalRect(0, 0, 8, 8)));
        still.Dispose();
        still.Dispose();
        Assert.False(still.HasImage);
        Assert.Throws<ObjectDisposedException>(() => still.Capture(gpu.Context, source));
    }

    [Fact]
    public void RepeatedCapture_OfTheSameSize_DoesNotAllocate()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var source = CreateSource(gpu.Device, (x, y) => (uint)(x ^ y) | 0xFF000000u);
        using var still = new StillFrame(gpu.Device, NullLogger.Instance);
        still.Capture(gpu.Context, source, new PhysicalRect(0, 0, 32, 32)); // warm-up: creates the texture
        still.Capture(gpu.Context, source, new PhysicalRect(1, 1, 32, 32));
        var view = still.ShaderResource;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            still.Capture(gpu.Context, source, new PhysicalRect(i % 32, i % 16, 32, 32));
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Same(view, still.ShaderResource);
    }

    private static ID3D11Texture2D CreateSource(ID3D11Device device, Func<int, int, uint> pixel)
    {
        var data = new uint[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                data[(y * Size) + x] = pixel(x, y);
            }
        }

        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            return device.CreateTexture2D(
                new Texture2DDescription(Format.B8G8R8A8_UNorm, Size, Size, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource),
                new SubresourceData(handle.AddrOfPinnedObject(), Size * 4));
        }
        finally
        {
            handle.Free();
        }
    }

    private static void Fill(ID3D11DeviceContext context, ID3D11Texture2D texture, uint color)
    {
        var data = Enumerable.Repeat(color, Size * Size).ToArray();
        context.UpdateSubresource(data, texture, 0, Size * 4);
    }

    private static uint[] Read(GpuDevice gpu, StillFrame still)
    {
        using var texture = still.ShaderResource!.Resource.QueryInterface<ID3D11Texture2D>();
        using var staging = gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)still.Width, (uint)still.Height, arraySize: 1, mipLevels: 1,
            bindFlags: BindFlags.None, usage: ResourceUsage.Staging, cpuAccessFlags: CpuAccessFlags.Read));
        gpu.Context.CopyResource(staging, texture);
        var mapped = gpu.Context.Map(staging, 0, MapMode.Read);
        try
        {
            var pixels = new uint[still.Width * still.Height];
            var all = mapped.AsSpan<uint>(staging, 0, 0);
            for (var y = 0; y < still.Height; y++)
            {
                all.Slice((int)(y * mapped.RowPitch / 4), still.Width).CopyTo(pixels.AsSpan(y * still.Width));
            }

            return pixels;
        }
        finally
        {
            gpu.Context.Unmap(staging, 0);
        }
    }
}
