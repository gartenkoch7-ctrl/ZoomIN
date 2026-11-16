using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Tracking;
using ZoomIN.Platform.Automation;

namespace ZoomIN.Platform.Tests.Automation;

/// <summary>Smoke test: the polling thread starts, talks to UI Automation and stops cleanly.</summary>
public sealed class UiaTrackingSourceTests
{
    [Fact]
    public void StartAndDispose_DoesNotThrow_AndStops()
    {
        var model = new TrackingModel(TrackingOptions.Default);
        var source = new UiaTrackingSource(NullLogger<UiaTrackingSource>.Instance);

        var exception = Record.Exception(() =>
        {
            source.Start(model);
            Thread.Sleep(200);
            source.Dispose();
        });

        Assert.Null(exception);
    }
}
