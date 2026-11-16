using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Renders a generated 128 × 128 image through the smart inversion on WARP (one frame, no memory).</summary>
internal static class SmartInvertRender
{
    public const int Size = 128;

    public static readonly Bgra White = new(255, 255, 255, 255);
    public static readonly Bgra Black = new(0, 0, 0, 255);
    public static readonly Bgra Bar = new(32, 32, 32, 255);
    public static readonly PhysicalRect Screen = new(0, 0, Size, Size);
    public static readonly DisplayEffects SmartMonitor =
        new(new FilterSettings(true, FilterId.InvertedSmart, 1, 0), Smoothing.Pixel, PointerStyle.Default);

    public static int Luma(Bgra c) => (int)((0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B));

    public static PixelBuffer Render(Func<int, int, Bgra> image, DisplayEffects effects, WindowRegions? windows = null)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var texture = TestImage.Create(gpu.Device, Size, image);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        using var ambient = new AmbientPass(gpu.Device, Size, Size);
        Upload(gpu, pass, effects, windows ?? WindowRegions.Empty);

        ambient.Update(gpu.Context, texture.View, 1, effects.ProtectPhotos);
        pass.Draw(gpu.Context, texture.View, target.View, Size, Size,
            MagnifyConstants.Fullscreen(Screen, Screen, effects.Matrix, Smoothing.Pixel, effects.IsSmart), ambient.Weights);
        return target.ReadBack(gpu.Device, gpu.Context);
    }

    public static void Upload(GpuDevice gpu, MagnifyPass pass, DisplayEffects effects, WindowRegions windows)
    {
        var constants = default(WindowConstants);
        WindowConstants.Fill(ref constants, windows, Screen, effects);
        pass.UpdateWindows(gpu.Context, constants);
    }
}
