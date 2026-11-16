using System.IO.Compression;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Tests.Folders;

namespace ZoomIN.Platform.Tests.Diagnostics;

public sealed class DiagnosticsPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ZoomIN-PackageTests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Package_HoldsLogsAndInfo_ButNotTheExportAddress()
    {
        var options = TelemetryOptions.Default with { OtlpEndpoint = "http://collector.intern:4317" };
        using var host = TelemetryHost.Start(TelemetryRole.App, null, Path.Combine(_root, "logs"), TimeProvider.System);
        host.Logger<DiagnosticsPackageTests>().Warning("written before packing");

        var path = new DiagnosticsPackage(host, new TestFolders(_root)).Create(options).Path;

        using var zip = ZipFile.OpenRead(path);
        var log = Assert.Single(zip.Entries, entry => entry.Name.EndsWith(".log", StringComparison.Ordinal));
        Assert.Contains("written before packing", Read(log), StringComparison.Ordinal);
        var info = Read(Assert.Single(zip.Entries, entry => entry.Name == "ZoomIN-Info.txt"));
        Assert.Contains("Recording: Normal", info, StringComparison.Ordinal);
        Assert.Contains("Export address set: True", info, StringComparison.Ordinal);
        Assert.DoesNotContain("collector.intern", info, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheNewestPackages_AreKept()
    {
        var packages = Directory.CreateDirectory(Path.Combine(_root, "diagnostics")).FullName;
        for (var i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(packages, $"ZoomIN-Diagnose-2026010{i}-120000.zip"), "old");
        }

        using var host = TelemetryHost.Start(TelemetryRole.App, null, Path.Combine(_root, "logs"), TimeProvider.System);
        var path = new DiagnosticsPackage(host, new TestFolders(_root)).Create(TelemetryOptions.Default).Path;

        var left = Directory.GetFiles(packages).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(DiagnosticsPackage.Kept, left.Count);
        Assert.Contains(Path.GetFileName(path), left);
    }

    [Fact]
    public void SecureLogs_AreAddedUnderSecure_AndTheCopyIsRemoved()
    {
        using var host = TelemetryHost.Start(TelemetryRole.App, null, Path.Combine(_root, "logs"), TimeProvider.System);
        var request = new FakeSecureLogs(SecureLogsResult.Copied);

        var path = new DiagnosticsPackage(host, new TestFolders(_root), request).Create(TelemetryOptions.Default, includeSecureLogs: true).Path;

        using var zip = ZipFile.OpenRead(path);
        Assert.Equal("secure line", Read(Assert.Single(zip.Entries, e => e.FullName == "secure/logs-zoomin.log")));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.EndsWith(".png", StringComparison.Ordinal));
        Assert.Contains("Secure desktop logs: included, 1 file(s)", Read(zip.GetEntry("ZoomIN-Info.txt")!), StringComparison.Ordinal);
        Assert.StartsWith(Path.Combine(_root, "diagnostics", "secure-logs-"), request.Target, StringComparison.Ordinal);
        Assert.False(Directory.Exists(request.Target));
    }

    [Theory]
    [InlineData(SecureLogsResult.Declined, "the administrator prompt was declined")]
    [InlineData(SecureLogsResult.Failed, "copying them failed")]
    public void WithoutTheCopy_TheInfoSaysWhy(SecureLogsResult result, string why)
    {
        using var host = TelemetryHost.Start(TelemetryRole.App, null, Path.Combine(_root, "logs"), TimeProvider.System);

        var path = new DiagnosticsPackage(host, new TestFolders(_root), new FakeSecureLogs(result)).Create(TelemetryOptions.Default, includeSecureLogs: true).Path;

        using var zip = ZipFile.OpenRead(path);
        Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("secure/", StringComparison.Ordinal));
        Assert.Contains(why, Read(zip.GetEntry("ZoomIN-Info.txt")!), StringComparison.Ordinal);
    }

    [Fact]
    public void SecureLogs_AreOnlyAskedFor_WhenChosen()
    {
        using var host = TelemetryHost.Start(TelemetryRole.App, null, Path.Combine(_root, "logs"), TimeProvider.System);
        var request = new FakeSecureLogs(SecureLogsResult.Copied);
        var package = new DiagnosticsPackage(host, new TestFolders(_root), request);

        Assert.Null(package.Create(TelemetryOptions.Default).SecureLogs);

        Assert.True(package.CanIncludeSecureLogs);
        Assert.Null(request.Target);
        Assert.False(new DiagnosticsPackage(host, new TestFolders(_root)).CanIncludeSecureLogs);
    }

    private static string Read(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }
}
