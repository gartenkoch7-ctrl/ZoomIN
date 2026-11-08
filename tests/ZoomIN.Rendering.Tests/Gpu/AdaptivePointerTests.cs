using Vortice.Mathematics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// The adaptive pointer (M15) on WARP, through the real layer: backdrop copy, desktop and the
/// colour rule of Cursor.hlsl.
/// </summary>
public sealed class AdaptivePointerTests
{
    private const int Size = 64;
    private static readonly Bgra s_white = new(255, 255, 255, 255);
    private static readonly Bgra s_black = new(0, 0, 0, 255);
    private static readonly PointerStyle s_style = PointerStyle.Default with { ColorMode = PointerColorMode.Adaptive, Rim = false };

    [Fact]
    public void TransparentOverlay_OverBlackDesktop_PointerIsWhite()
    {
        var pixels = Draw((_, _) => s_black, overlay: null, s_style);

        Assert.Equal(s_white, pixels.At(24, 24));
    }

    [Fact]
    public void TransparentOverlay_OverWhiteDesktop_PointerIsBlack()
    {
        var pixels = Draw((_, _) => s_white, overlay: null, s_style);

        Assert.Equal(s_black, pixels.At(24, 24));
    }

    [Fact]
    public void DrawnOverlay_WinsOverTheDesktop()
    {
        // Zoomed or filtered: the overlay is opaque white although the desktop below is black.
        var pixels = Draw((_, _) => s_black, overlay: new Color4(1, 1, 1, 1), s_style);

        Assert.Equal(s_black, pixels.At(24, 24));
    }

    [Fact]
    public void EdgeUnderThePointer_EachPartContrastsWithItsGround()
    {
        var pixels = Draw((x, _) => x < 32 ? s_black : s_white, overlay: null, s_style);

        Assert.Equal(s_white, pixels.At(20, 24));
        Assert.Equal(s_black, pixels.At(44, 24));
    }

    [Fact]
    public void Outline_TakesTheOtherColourOfThePair()
    {
        var style = s_style with { AdaptiveOnDark = new RgbColor(255, 255, 0), AdaptiveOnLight = new RgbColor(0, 0, 255) };

        // Left half of the pointer image white (body), right half black (outline), on dark ground.
        var pixels = Draw((_, _) => s_black, overlay: null, style, (x, _) => x < 16 ? (byte)255 : (byte)0);

        Assert.Equal(new Bgra(0, 255, 255, 255), pixels.At(24, 24)); // body: yellow
        Assert.Equal(new Bgra(255, 0, 0, 255), pixels.At(40, 24)); // outline: blue
    }

    [Theory]
    [InlineData(0.5, 255)]
    [InlineData(0.3, 0)]
    public void Threshold_DecidesForGrey(double threshold, byte expected)
    {
        var grey = new Bgra(102, 102, 102, 255); // luma 0.4
        var pixels = Draw((_, _) => grey, overlay: null, s_style with { AdaptiveThreshold = threshold });

        Assert.Equal(new Bgra(expected, expected, expected, 255), pixels.At(24, 24));
    }

    [Theory]
    [InlineData(PointerColorMode.Original, true)]
    [InlineData(PointerColorMode.Adaptive, false)]
    public void Docked_AdaptivePointerReplacesTheSystemPointer(PointerColorMode mode, bool systemVisible)
    {
        var systemCursor = new NoSystemCursor();

        // Dock strip on the top 40 %, mouse below it in the rest of the screen.
        var pixels = Draw((_, _) => s_black, overlay: null, s_style with { ColorMode = mode, GrowWithZoom = false },
            mode: ZoomMode.Docked, zoom: 2, cursor: new PhysicalPoint(16, 30), systemCursor: systemCursor);

        Assert.Equal(systemVisible, systemCursor.Visible);
        if (mode == PointerColorMode.Adaptive)
        {
            Assert.Equal(s_white, pixels.At(20, 60)); // drawn at the real position, white on black
        }
    }

    // A 32×32 pointer with its hotspot at the top-left, drawn at (16, 16) at 1× unless told otherwise.
    private static PixelBuffer Draw(Func<int, int, Bgra> desktopPixel, Color4? overlay, PointerStyle style,
        Func<int, int, byte>? pointerValue = null, ZoomMode mode = ZoomMode.Fullscreen, double zoom = 1,
        PhysicalPoint? cursor = null, NoSystemCursor? systemCursor = null)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var desktop = TestImage.Create(gpu.Device, Size, desktopPixel);
        using var pass = new CursorPass(gpu.Device);
        using var backdrop = new PointerBackdrop(gpu.Device);
        var layer = new CursorLayer(pass, backdrop, new ShapeSource(pointerValue ?? ((_, _) => 255)), systemCursor ?? new NoSystemCursor());
        var monitor = new PhysicalRect(0, 0, Size, Size);
        var at = cursor ?? new PhysicalPoint(16, 16);
        var frame = ModeGeometry.Compute(mode, monitor, at, zoom, LensSize.Default, DockLayout.Default);
        gpu.Context.ClearRenderTargetView(target.View, overlay ?? new Color4(0, 0, 0, 0));

        layer.Draw(gpu.Context, new LayerTarget(target.View, target.Texture, Size, Size, desktop.View), monitor, frame,
            mode, zoom, at, new DisplayEffects(FilterSettings.Default, Smoothing.Text, style), 0, hidden: false);
        return target.ReadBack(gpu.Device, gpu.Context);
    }

    private sealed class NoSystemCursor : ISystemCursor
    {
        public bool Visible { get; private set; } = true;

        public void SetVisible(bool visible) => Visible = visible;

        public void Dispose()
        {
        }
    }

    private sealed class ShapeSource(Func<int, int, byte> value) : ICursorShapeSource
    {
        private const int Side = 32;

        public bool TryGetCurrent(out nint handle)
        {
            handle = 1;
            return true;
        }

        public nint ArrowHandle() => 1;

        public CursorShape? Load(nint handle)
        {
            var pixels = new byte[Side * Side * 4];
            for (var y = 0; y < Side; y++)
            {
                for (var x = 0; x < Side; x++)
                {
                    var v = value(x, y);
                    var i = ((y * Side) + x) * 4;
                    (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (v, v, v, 255);
                }
            }

            return new CursorShape(handle, Side, Side, 0, 0, pixels);
        }

        public CursorShape? LoadStandard(nint handle, double scale) => Load(handle);
    }
}
