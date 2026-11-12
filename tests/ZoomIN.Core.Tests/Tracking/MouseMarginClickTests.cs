using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using static ZoomIN.Core.Tests.Tracking.MouseMarginTests;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>
/// Clicks with the margin (ADR-0018 still holds): the overlay is click-through, so a click lands on
/// the real mouse point. It hits what the user sees only if that point is in the view, where the
/// pointer is drawn – on every frame, over the whole monitor, also while zooming and after jumps.
/// </summary>
public sealed class MouseMarginClickTests
{
    [Theory]
    [InlineData(ZoomMode.Fullscreen)]
    [InlineData(ZoomMode.Lens)]
    public void ThePointShownUnderThePointer_IsTheMousePoint_AllOverTheMonitor(ZoomMode mode)
    {
        var controller = new AnchorController();
        var frames = 0;
        var zoom = 4.0;
        for (var y = 0.0; y < 1080; y += 41)
        {
            // Serpentine, so the view is pushed in all four directions.
            for (var i = 0; i < 1920 / 23; i++)
            {
                var x = (y / 41 % 2 == 0) ? i * 23.0 : 1919 - (i * 23.0);
                zoom = 2 + ((frames / 50) % 6);
                var mouse = new PhysicalPoint(x, y);
                var frame = Frame(controller.Next(Input(1.0 + (frames++ / 60.0), mouse, mode, zoom)), mode, zoom);

                Assert.True(frame.Source.Contains(mouse), $"{mouse} is outside the view {frame.Source} at {zoom}×");
            }
        }
    }

    [Theory]
    [InlineData(ZoomMode.Fullscreen)]
    [InlineData(ZoomMode.Lens)]
    public void AJumpOfTheMouse_IsInViewInTheSameFrame(ZoomMode mode)
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, new PhysicalPoint(100, 100), mode));
        PhysicalPoint[] jumps = [new(1900, 1000), new(10, 1070), new(960, 540), new(1919, 0)];

        for (var i = 0; i < jumps.Length; i++)
        {
            var frame = Frame(controller.Next(Input(1.1 + (i / 10.0), jumps[i], mode)), mode);
            Assert.True(frame.Source.Contains(jumps[i]), $"{jumps[i]} is outside the view {frame.Source}");
        }
    }
}
