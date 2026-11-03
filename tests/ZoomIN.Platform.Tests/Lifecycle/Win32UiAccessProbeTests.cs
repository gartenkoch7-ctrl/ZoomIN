using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.Platform.Tests.Lifecycle;

public sealed class Win32UiAccessProbeTests
{
    [Fact]
    public void TestProcess_HasNoUiAccess()
    {
        // The test host is neither signed nor in Program Files, so Windows never grants UIAccess.
        Assert.False(new Win32UiAccessProbe().IsActive);
    }
}
