using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.PointerFence;

namespace ZoomIN.Platform.Tests.PointerFence;

/// <summary>The mover's logic with a fake SetCursorPos: the real pointer is never moved by tests.</summary>
public sealed class PointerMoverTests
{
    [Fact]
    public void MoveTo_FloorsToWholePixels_AndReportsSuccess()
    {
        PhysicalPoint? moved = null;
        var mover = new Win32PointerMover(NullLogger.Instance, new FakeMonitors(), point =>
        {
            moved = point;
            return (true, 0);
        });

        Assert.True(mover.MoveTo(new PhysicalPoint(1919.6, 540.2), PointerMoveReason.FreezeMonitor));
        Assert.Equal(new PhysicalPoint(1919, 540), moved);
    }

    [Fact]
    public void MoveTo_Refused_ReturnsFalse()
    {
        var mover = new Win32PointerMover(NullLogger.Instance, new FakeMonitors(), _ => (false, 5));

        Assert.False(mover.MoveTo(new PhysicalPoint(10, 10), PointerMoveReason.NextMonitor));
    }
}
