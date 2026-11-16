using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// "Schärfe"/"Fett" (M18, D1) on WARP: the middle 32 × 32 pixels of a generated 64 × 64 desktop
/// magnified 8× into a 256 × 256 target, with text-optimised smoothing and an optional filter.
/// </summary>
internal static class EnhanceRender
{
    public const int Image = 64;
    public const int Target = 256;
    public const int Zoom = 8;

    public static readonly PhysicalRect Screen = new(0, 0, Image, Image);
    public static readonly PhysicalRect Source = new(16, 16, 32, 32);

    public static DisplayEffects Effects(FilterId? filter, Smoothing smoothing = Smoothing.Text) =>
        new(new FilterSettings(filter is not null, filter ?? FilterId.Inverted, 1, 0), smoothing, PointerStyle.Default);

    /// <param name="smoothing">Pixel draws the exact colours of the image: the range a halo would leave.</param>
    public static PixelBuffer Render(Func<int, int, Bgra> image, TextEnhancement enhance, FilterId? filter = null, Smoothing smoothing = Smoothing.Text) =>
        Render(image, enhance, Effects(filter, smoothing));

    /// <param name="effects">Any effects, e.g. with own schemes (M18 integration: C and D1 in one pass).</param>
    public static PixelBuffer Render(Func<int, int, Bgra> image, TextEnhancement enhance, DisplayEffects effects)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var texture = TestImage.Create(gpu.Device, Image, image);
        using var target = RenderTarget.Create(gpu.Device, Target);
        using var pass = new MagnifyPass(gpu.Device);
        using var ambient = new AmbientPass(gpu.Device, Image, Image);
        var windows = default(WindowConstants);
        WindowConstants.Fill(ref windows, WindowRegions.Empty, Screen, effects);
        pass.UpdateWindows(gpu.Context, windows);
        if (effects.IsSmart)
        {
            ambient.Update(gpu.Context, texture.View, 1, effects.ProtectPhotos);
        }

        var constants = MagnifyConstants.Fullscreen(
            Source, Screen, effects.Matrix, effects.Smoothing, effects.IsSmart, enhance.At(Zoom, effects.Smoothing));
        pass.Draw(gpu.Context, texture.View, target.View, Target, Target, constants, effects.IsSmart ? ambient.Weights : null);
        return target.ReadBack(gpu.Device, gpu.Context);
    }

    public static int Luma(Bgra c) => (int)Math.Round((0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B));

    public static int Chroma(Bgra c) => Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));

    /// <summary>Luma of every pixel of row <paramref name="y"/>.</summary>
    public static int[] Row(PixelBuffer pixels, int y)
    {
        var row = new int[Target];
        for (var x = 0; x < Target; x++)
        {
            row[x] = Luma(pixels.At(x, y));
        }

        return row;
    }

    /// <summary>Lowest and highest luma of the whole target.</summary>
    public static (int Low, int High) Range(PixelBuffer pixels)
    {
        var (low, high) = (255, 0);
        for (var y = 0; y < Target; y++)
        {
            foreach (var luma in Row(pixels, y))
            {
                low = Math.Min(low, luma);
                high = Math.Max(high, luma);
            }
        }

        return (low, high);
    }
}
