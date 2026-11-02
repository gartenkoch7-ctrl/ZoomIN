using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Tests.Pointer;

public sealed class PointerStyleTests
{
    [Fact]
    public void Default_LeavesWindowsPointerAloneWithoutZoom()
    {
        var style = PointerStyle.Default;

        Assert.False(style.DrawsPointer(1.0));
        Assert.False(style.NeedsOverlayUnzoomed);
        Assert.True(style.DrawsPointer(2.0));
        Assert.Equal(LocatorTriggers.Hotkey | LocatorTriggers.Shake | LocatorTriggers.AfterIdle, style.LocatorTriggers);
    }

    [Fact]
    public void UnzoomedScale_AboveOne_DrawsPointerAndIsMultipliedByTheZoomUpToMax()
    {
        var style = PointerStyle.Default with { UnzoomedScale = 1.5, MaxScale = 4 };

        Assert.True(style.NeedsOverlayUnzoomed);
        Assert.Equal(1.5, style.ScaleFor(1.0));
        Assert.Equal(3.0, style.ScaleFor(2.0));
        Assert.Equal(4.0, style.ScaleFor(8.0));
    }

    [Fact]
    public void UnzoomedScale_AboveMax_IsNeverShrunkWhileZoomed()
    {
        var style = PointerStyle.Default with { UnzoomedScale = 3, MaxScale = 2 };

        Assert.Equal(3.0, style.ScaleFor(4.0));
    }

    [Fact]
    public void GrowWithZoomOff_KeepsTheUnzoomedSizeAtEveryZoom()
    {
        var style = PointerStyle.Default with { UnzoomedScale = 2, GrowWithZoom = false };

        Assert.Equal(2.0, style.ScaleFor(1.0));
        Assert.Equal(2.0, style.ScaleFor(6.0));
        Assert.Equal(1.0, (PointerStyle.Default with { GrowWithZoom = false }).ScaleFor(4.0));
    }

    [Fact]
    public void OverrideWindows_DrawsThePointerEvenWithoutZoom()
    {
        Assert.False(PointerStyle.Default.OverrideWindows);
        Assert.True((PointerStyle.Default with { OverrideWindows = true }).NeedsOverlayUnzoomed);
    }

    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(1.2, 2)]
    [InlineData(2.0, 2)]
    [InlineData(3.5, 4)]
    [InlineData(6.0, 8)]
    [InlineData(20.0, 8)]
    public void SharpScale_RoundsUpToPowersOfTwo(double scale, int sharp)
    {
        Assert.Equal(sharp, PointerStyle.SharpScale(scale));
    }

    [Fact]
    public void ColourMode_DrawsPointerWithoutZoom()
    {
        Assert.True((PointerStyle.Default with { ColorMode = PointerColorMode.Inverted }).NeedsOverlayUnzoomed);
    }

    [Theory]
    [InlineData(false, false, 1.0, false)]
    [InlineData(true, false, 1.0, false)]
    [InlineData(true, false, 2.0, true)]
    [InlineData(true, true, 1.0, true)]
    public void Crosshair_OnlyWhileZoomedUnlessAlways(bool enabled, bool always, double zoom, bool visible)
    {
        var style = PointerStyle.Default with { CrosshairEnabled = enabled, CrosshairAlways = always };

        Assert.Equal(visible, style.CrosshairVisible(zoom));
    }

    [Fact]
    public void Normalized_ClampsHandEditedValues()
    {
        var style = new PointerStyle { UnzoomedScale = 9, RimWidth = 0, LocatorSeconds = 99, CrosshairOpacity = 0 }.Normalized();

        Assert.Equal(4, style.UnzoomedScale);
        Assert.Equal(1, style.RimWidth);
        Assert.Equal(3, style.LocatorSeconds);
        Assert.Equal(0.2, style.CrosshairOpacity);
    }

    [Fact]
    public void RgbColor_RoundTripsHex_AndFallsBackOnGarbage()
    {
        Assert.Equal(new RgbColor(0x12, 0xAB, 0xEF), RgbColor.ParseOrDefault("#12ABEF", RgbColor.Black));
        Assert.Equal("#12ABEF", new RgbColor(0x12, 0xAB, 0xEF).ToHex());
        Assert.Equal(RgbColor.Black, RgbColor.ParseOrDefault("yellow", RgbColor.Black));
        Assert.Equal(RgbColor.Black, RgbColor.ParseOrDefault(null, RgbColor.Black));
    }

    [Fact]
    public void HideWhileTyping_HidesUntilTheMouseMoves_OnlyWhenEnabled()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        var signals = 0;
        model.Signaled += () => signals++;

        model.MarkTyped();
        Assert.False(model.PointerHiddenByTyping);

        model.SetCursor(PointerStyle.Default with { HideWhileTyping = true });
        model.MarkTyped();
        model.MarkTyped();
        Assert.True(model.PointerHiddenByTyping);

        model.MarkMoved();
        Assert.False(model.PointerHiddenByTyping);
        Assert.Equal(2, signals);
    }

    [Fact]
    public void ToggleCrosshair_SwitchesAndSaves()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        var changed = 0;
        model.Changed += () => changed++;

        model.ToggleCrosshair();

        Assert.True(model.Current.Cursor.CrosshairEnabled);
        Assert.Equal(1, changed);
    }
}
