using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.Tests.Snapshots;

/// <summary>The snapshot text tells an agent whether "Schärfe"/"Fett" were applied (M18, D1).</summary>
public sealed class SnapshotInfoTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(4.0, Smoothing.Text, "applied in this view True")]
    [InlineData(1.0, Smoothing.Text, "applied in this view False")]
    [InlineData(4.0, Smoothing.Pixel, "applied in this view False")]
    public void Describe_NamesSmoothingAndEnhancement(double zoom, Smoothing smoothing, string applied)
    {
        var effects = DisplayEffects.Default with { Smoothing = smoothing, Enhancement = new TextEnhancement(0.6, 1.5) };
        var request = new FrameRequest(
            ZoomMode.Fullscreen, zoom, LensSize.Default, DockLayout.Default, effects, 0, TrackingSnapshot.Empty, TrackingOptions.Default, false,
            WindowRegions.Empty, false);

        var frame = new ModeFrame(s_monitor, new PhysicalRect(0, 0, s_monitor.Width / zoom, s_monitor.Height / zoom), false);

        var text = SnapshotInfo.Describe(@"\\.\DISPLAY1", s_monitor, frame, request, false);

        Assert.Contains($"Smoothing: {smoothing}, sharpness 60 %, bold 1.5 px, {applied}", text, StringComparison.Ordinal);
    }
}
