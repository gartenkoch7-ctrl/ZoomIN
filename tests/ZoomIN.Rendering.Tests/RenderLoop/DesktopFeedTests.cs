using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>When the desktop of a monitor is captured (M15, ADR-0026).</summary>
public sealed class DesktopFeedTests
{
    private static readonly DisplayEffects s_adaptive = DisplayEffects.Default with
    {
        Cursor = PointerStyle.Default with { ColorMode = PointerColorMode.Adaptive },
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Zoomed_AlwaysCaptures(bool pointerHere)
    {
        Assert.True(DesktopFeed.Needed(Request(2, DisplayEffects.Default), pointerHere));
    }

    [Fact]
    public void Unzoomed_WithoutAdaptivePointer_DoesNotCapture()
    {
        Assert.False(DesktopFeed.Needed(Request(1, DisplayEffects.Default), pointerHere: true));
    }

    [Fact]
    public void Unzoomed_AdaptivePointer_CapturesOnlyWhereTheMouseIs()
    {
        Assert.True(DesktopFeed.Needed(Request(1, s_adaptive), pointerHere: true));
        Assert.False(DesktopFeed.Needed(Request(1, s_adaptive), pointerHere: false));
    }

    private static FrameRequest Request(double zoom, DisplayEffects effects) => new(
        ZoomMode.Fullscreen, zoom, LensSize.Default, DockLayout.Default, effects, 0, TrackingSnapshot.Empty,
        TrackingOptions.Default, false, WindowRegions.Empty, false);
}
