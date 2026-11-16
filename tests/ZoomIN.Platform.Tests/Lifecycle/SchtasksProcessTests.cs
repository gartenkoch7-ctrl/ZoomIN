using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>The real schtasks.exe, read-only: a query for a task that cannot exist.</summary>
public sealed class SchtasksProcessTests
{
    [Fact]
    public void QueryOfAMissingTask_IsNotZero()
    {
        var exitCode = new SchtasksProcess().Run(["/Query", "/TN", "ZoomIN Tests " + Guid.NewGuid().ToString("N")]);

        Assert.NotEqual(0, exitCode);
    }
}
