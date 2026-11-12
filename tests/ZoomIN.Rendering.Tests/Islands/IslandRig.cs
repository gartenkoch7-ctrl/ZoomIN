using Microsoft.Extensions.Logging.Abstractions;
using Vortice.Mathematics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Islands;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Tests.Gpu;

namespace ZoomIN.Rendering.Tests.Islands;

/// <summary>
/// An <see cref="IslandLayer"/> on WARP over the 64×64 test image (left half red with a green
/// corner, right half blue), drawing into a cleared, transparent target.
/// </summary>
internal sealed class IslandRig : IDisposable
{
    public const int Size = 64;
    public const string Monitor = @"\\.\ISLANDS";

    public static readonly PhysicalRect Bounds = new(0, 0, Size, Size);

    public IslandRig()
    {
        Gpu = GpuDevice.CreateWarp();
        Image = TestImage.Create(Gpu.Device, Size);
        Target = RenderTarget.Create(Gpu.Device, Size);
        Pass = new MagnifyPass(Gpu.Device);
        Marks = new CursorPass(Gpu.Device);
        Stills = new IslandStills(Gpu.Device, NullLogger.Instance);
        Time = new GpuPassTimer(Gpu.Device, Monitor, "islands");
        Layer = new IslandLayer(Pass, Marks, Stills, Time);
    }

    public GpuDevice Gpu { get; }

    public TestImage Image { get; }

    public RenderTarget Target { get; }

    public MagnifyPass Pass { get; }

    public CursorPass Marks { get; }

    public IslandLayer Layer { get; }

    public IslandStills Stills { get; }

    public GpuPassTimer Time { get; }

    public static DisplayEffects Effects(FilterId? filter = null) =>
        new(new FilterSettings(filter is not null, filter ?? FilterId.Inverted, 1, 0), Smoothing.Pixel, PointerStyle.Default);

    /// <summary>One island at 2×: source 16×16 at (<paramref name="x"/>, <paramref name="y"/>), picture at (8, 24).</summary>
    public static Island At(double x, double y, bool still = false) =>
        new(0, Monitor, new PhysicalRect(x, y, 16, 16), new PhysicalPoint(8, 24), 2, still);

    public static IslandScene Scene(IslandEditView? edit, params Island[] islands)
    {
        var set = IslandSet.Empty;
        foreach (var island in islands)
        {
            set = set.Add(island)!;
        }

        return new IslandScene(set, true, edit);
    }

    public PixelBuffer Draw(IslandScene scene, DisplayEffects? effects = null, TestImage? image = null)
    {
        Gpu.Context.ClearRenderTargetView(Target.View, new Color4(0, 0, 0, 0));
        var source = image ?? Image;
        Layer.Update(scene, System.Diagnostics.Stopwatch.GetTimestamp());
        Layer.Draw(Gpu.Context, source.View, source.Texture, Target.View, Monitor, Bounds, scene, effects ?? Effects(), null);
        return Target.ReadBack(Gpu.Device, Gpu.Context);
    }

    public void Dispose()
    {
        Time.Dispose();
        Stills.Dispose();
        Marks.Dispose();
        Pass.Dispose();
        Target.Dispose();
        Image.Dispose();
        Gpu.Dispose();
    }
}
