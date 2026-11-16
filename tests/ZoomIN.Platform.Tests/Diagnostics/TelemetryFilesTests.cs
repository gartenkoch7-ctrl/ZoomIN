using ZoomIN.Platform.Diagnostics;

namespace ZoomIN.Platform.Tests.Diagnostics;

/// <summary>M11: at most 20 MB of logs per day; beyond that only errors are written (ADR-0019).</summary>
public sealed class TelemetryFilesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ZoomIN-TelemetryFiles-").FullName;

    [Fact]
    public void DailyLimit_DropsNormalText_ButKeepsErrors()
    {
        var files = new TelemetryFiles(_folder, TimeProvider.System);
        var path = files.LogFile(TimeProvider.System.GetLocalNow());

        Assert.True(files.Append(path, new string('x', (int)TelemetryFiles.DailyLimitBytes - 10), essential: false));
        Assert.False(files.Append(path, "warning over the limit\n", essential: false));
        Assert.True(files.Append(path, "error over the limit\n", essential: true));

        var text = File.ReadAllText(path);
        Assert.DoesNotContain("warning over the limit", text, StringComparison.Ordinal);
        Assert.Contains("error over the limit", text, StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
