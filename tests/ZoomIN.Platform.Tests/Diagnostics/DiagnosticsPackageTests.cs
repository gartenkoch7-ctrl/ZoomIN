using System.IO.Compression;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Platform.Diagnostics;

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

        var path = DiagnosticsPackage.Create(host, options, Path.Combine(_root, "packages"));

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
        var packages = Directory.CreateDirectory(Path.Combine(_root, "packages")).FullName;
        for (var i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(packages, $"ZoomIN-Diagnose-2026010{i}-120000.zip"), "old");
        }

        using var host = TelemetryHost.Start(TelemetryRole.App, null, Path.Combine(_root, "logs"), TimeProvider.System);
        var path = DiagnosticsPackage.Create(host, TelemetryOptions.Default, packages);

        var left = Directory.GetFiles(packages).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(DiagnosticsPackage.Kept, left.Count);
        Assert.Contains(Path.GetFileName(path), left);
    }

    private static string Read(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }
}
