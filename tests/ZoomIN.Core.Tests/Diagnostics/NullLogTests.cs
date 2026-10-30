using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Tests.Diagnostics;

public sealed class NullLogTests
{
    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(NullLog.Instance, NullLog.Instance);
    }

    [Fact]
    public void AllMethods_DoNotThrow()
    {
        var exception = Record.Exception(() => WriteAll(NullLog.Instance));

        Assert.Null(exception);
    }

    // Goes through the interface on purpose: callers only ever see ILog.
    private static void WriteAll(ILog log)
    {
        log.Info("info");
        log.Warn("warn");
        log.Error("error", new InvalidOperationException("test"));
    }
}
