using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

public sealed class WindowKindTests
{
    [Fact]
    public void NormalApp_CanHaveARule()
    {
        Assert.True(new WindowKind("winword.exe", "OpusApp").CanHaveRule);
    }

    [Fact]
    public void UnreadableProgram_CannotHaveARule_BecauseTheNameStandsForManyApps()
    {
        Assert.False(new WindowKind(WindowKind.UnknownProcess, "#32770").CanHaveRule);
    }

    [Fact]
    public void StoreFrameWithoutItsApp_CannotHaveARule_ButTheAppCan()
    {
        var frame = new WindowKind("ApplicationFrameHost.exe", "ApplicationFrameWindow");
        var app = new WindowKind("systemsettings.exe", "ApplicationFrameWindow");

        Assert.True(frame.IsStoreFrame);
        Assert.False(frame.CanHaveRule);
        Assert.False(app.IsStoreFrame);
        Assert.True(app.CanHaveRule);
    }
}
