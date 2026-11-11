using System.Globalization;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// D1 spike (never merged): comparison sheets on WARP for a human look. Each sheet is one spot of
/// <see cref="D1SpikeScene"/> at one zoom: rows = no filter / inverted / smart inverted, columns =
/// off / variant A (in pass) / variant B (post pass). Runs only with ZOOMIN_D1_OUT = output folder.
/// </summary>
public sealed class D1SpikeImageTests
{
    private const int TileWidth = 480;
    private const int TileHeight = 270;
    private static readonly ColorFilter?[] s_filters = [null, ColorFilter.Inverted, ColorFilter.InvertedSmart];
    private static readonly D1Variant[] s_variants = [D1Variant.Off, D1Variant.InPass, D1Variant.PostPass];

    [Fact]
    public void WriteComparisonSheets()
    {
        var output = Environment.GetEnvironmentVariable("ZOOMIN_D1_OUT");
        Assert.SkipWhen(string.IsNullOrEmpty(output), "Set ZOOMIN_D1_OUT to a folder for the comparison PNGs.");
        Directory.CreateDirectory(output!);

        using var gpu = GpuDevice.CreateWarp();
        using var scene = Upload(gpu.Device, D1SpikeScene.Create());
        using var view = gpu.Device.CreateShaderResourceView(scene);
        using var renderer = new D1SpikeRenderer(gpu, view, D1SpikeScene.Width, D1SpikeScene.Height, TileWidth, TileHeight);
        PngFile.Write(Path.Combine(output!, "d1-scene.png"), TextureReader.ReadBgra(gpu.Device, gpu.Context, scene));

        foreach (var (sharp, bold) in new[] { (0.6f, 1f), (1f, 3f) })
        {
            foreach (var zoom in new[] { 2.0, 4.0, 8.0 })
            {
                foreach (var (name, x, y) in D1SpikeScene.Spots)
                {
                    var sheet = Sheet(gpu, renderer, zoom, x, y, sharp, bold);
                    var file = string.Create(CultureInfo.InvariantCulture, $"d1-{name}-zoom{zoom}-s{sharp * 100:0}-b{bold:0}.png");
                    PngFile.Write(Path.Combine(output!, file), sheet);
                }
            }
        }
    }

    private static SnapshotImage Sheet(GpuDevice gpu, D1SpikeRenderer renderer, double zoom, int x, int y, float sharp, float bold)
    {
        const int Columns = 3;
        var width = TileWidth * Columns;
        var rgba = new byte[width * TileHeight * s_filters.Length * 4];
        for (var row = 0; row < s_filters.Length; row++)
        {
            var frame = renderer.Prepare(s_filters[row], zoom, x, y, sharp, bold);
            for (var column = 0; column < Columns; column++)
            {
                renderer.Draw(s_variants[column], frame);
                var tile = TextureReader.ReadBgra(gpu.Device, gpu.Context, renderer.TargetTexture);
                for (var line = 0; line < TileHeight; line++)
                {
                    var to = ((((row * TileHeight) + line) * width) + (column * TileWidth)) * 4;
                    Array.Copy(tile.Rgba, line * TileWidth * 4, rgba, to, TileWidth * 4);
                }
            }
        }

        return new SnapshotImage(width, TileHeight * s_filters.Length, rgba);
    }

    private static ID3D11Texture2D Upload(ID3D11Device device, byte[] bgra)
    {
        var handle = GCHandle.Alloc(bgra, GCHandleType.Pinned);
        try
        {
            return device.CreateTexture2D(
                new Texture2DDescription(Format.B8G8R8A8_UNorm, D1SpikeScene.Width, D1SpikeScene.Height, arraySize: 1, mipLevels: 1, bindFlags: BindFlags.ShaderResource),
                new SubresourceData(handle.AddrOfPinnedObject(), D1SpikeScene.Width * 4));
        }
        finally
        {
            handle.Free();
        }
    }
}
