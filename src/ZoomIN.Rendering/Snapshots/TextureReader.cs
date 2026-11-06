using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ZoomIN.Rendering.Snapshots;

/// <summary>
/// Reads a texture back to the CPU through a staging copy. Debug snapshots only: allocates and
/// waits for the GPU, which the render loop otherwise never does (AGENTS.md 2.3).
/// </summary>
internal static class TextureReader
{
    /// <summary>A B8G8R8A8 texture (desktop copy, swapchain back buffer) as RGBA.</summary>
    public static SnapshotImage ReadBgra(ID3D11Device device, ID3D11DeviceContext context, ID3D11Resource resource) =>
        Read(device, context, resource, Format.B8G8R8A8_UNorm, static (row, x, rgba, at) =>
        {
            rgba[at] = row[(x * 4) + 2];
            rgba[at + 1] = row[(x * 4) + 1];
            rgba[at + 2] = row[x * 4];
            rgba[at + 3] = 255;
        });

    /// <summary>The smart inversion weights (R16G16_FLOAT, x = weight) as grey: black = original, white = inverted.</summary>
    public static SnapshotImage ReadWeights(ID3D11Device device, ID3D11DeviceContext context, ID3D11Resource resource) =>
        Read(device, context, resource, Format.R16G16_Float, static (row, x, rgba, at) =>
        {
            var weight = (float)BitConverter.ToHalf(row.Slice(x * 4, 2));
            var grey = (byte)Math.Clamp(weight * 255f + 0.5f, 0f, 255f);
            rgba[at] = grey;
            rgba[at + 1] = grey;
            rgba[at + 2] = grey;
            rgba[at + 3] = 255;
        });

    private delegate void PixelCopy(ReadOnlySpan<byte> row, int x, byte[] rgba, int at);

    private static SnapshotImage Read(ID3D11Device device, ID3D11DeviceContext context, ID3D11Resource resource, Format expected, PixelCopy copy)
    {
        using var texture = resource.QueryInterface<ID3D11Texture2D>();
        var description = texture.Description;
        if (description.Format != expected)
        {
            throw new InvalidOperationException($"Unexpected texture format {description.Format}.");
        }

        using var staging = device.CreateTexture2D(new Texture2DDescription(
            description.Format, description.Width, description.Height, arraySize: 1, mipLevels: 1,
            bindFlags: BindFlags.None, usage: ResourceUsage.Staging, cpuAccessFlags: CpuAccessFlags.Read));
        context.CopyResource(staging, texture);

        var width = (int)description.Width;
        var height = (int)description.Height;
        var rgba = new byte[width * height * 4];
        var mapped = context.Map(staging, 0, MapMode.Read);
        try
        {
            var all = mapped.AsSpan<byte>(staging, 0, 0);
            for (var y = 0; y < height; y++)
            {
                var row = all.Slice((int)(y * mapped.RowPitch));
                for (var x = 0; x < width; x++)
                {
                    copy(row, x, rgba, ((y * width) + x) * 4);
                }
            }
        }
        finally
        {
            context.Unmap(staging, 0);
        }

        return new SnapshotImage(width, height, rgba);
    }
}
