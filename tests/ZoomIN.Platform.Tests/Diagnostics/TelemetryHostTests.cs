using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Platform.Diagnostics;

namespace ZoomIN.Platform.Tests.Diagnostics;

/// <summary>The telemetry host writes logs and operations to hourly files in its own folder.</summary>
public sealed class TelemetryHostTests : IDisposable
{
    private const string Parent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ZoomIN-TelemetryTests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Normal_WritesWarningsAndErrors_NotInfo()
    {
        using (var host = Start(TelemetryRole.App))
        {
            var log = host.Logger<TelemetryHostTests>();
            log.Info("info line");
            log.Warning("warning line");
            log.Failure(new InvalidOperationException("boom"), "error line");
        }

        var text = ReadAll("*.log");
        Assert.DoesNotContain("info line", text, StringComparison.Ordinal);
        Assert.Contains("[WARN ] app#", text, StringComparison.Ordinal);
        Assert.Contains("TelemetryHostTests[9003]: error line", text, StringComparison.Ordinal);
        Assert.Contains("boom", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorsOnly_DropsWarnings_KeepsErrors_WithoutRestart()
    {
        using (var host = Start(TelemetryRole.App))
        {
            host.Apply(TelemetryOptions.Default with { Level = TelemetryLevel.ErrorsOnly });
            var log = host.Logger<TelemetryHostTests>();
            log.Warning("warning line");
            log.Failure(null, "error line");
        }

        var text = ReadAll("*.log");
        Assert.DoesNotContain("warning line", text, StringComparison.Ordinal);
        Assert.Contains("error line", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Operation_ContinuesTheParentTrace_AndDropsUserContent()
    {
        using (var host = Start(TelemetryRole.Update, Parent))
        {
            using var operation = host.StartOperation("zoomin.test.run");
            Assert.NotNull(operation);
            operation.SetTag("zoomin.window.class", "Notepad");
            operation.SetTag("window.title", "Secret.docx - Word");
            host.Logger<TelemetryHostTests>().Warning("inside the operation");
        }

        var trace = ReadAll("*.trace.jsonl");
        Assert.Contains("\"name\":\"zoomin.test.run\"", trace, StringComparison.Ordinal);
        Assert.Contains("\"trace\":\"0af7651916cd43dd8448eb211c80319c\"", trace, StringComparison.Ordinal);
        Assert.Contains("\"parent\":\"b7ad6b7169203331\"", trace, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"update\"", trace, StringComparison.Ordinal);
        Assert.Contains("Notepad", trace, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", trace, StringComparison.Ordinal);
        var log = ReadAll("*.log");
        Assert.True(log.Contains("trace=0af7651916cd43dd8448eb211c80319c", StringComparison.Ordinal), "LOG: " + log + " TRACE: " + trace);
    }

    [Fact]
    public void TracesOff_WritesNoOperation()
    {
        // Not "StartOperation returns null": the test platform listens to every activity source.
        using (var host = Start(TelemetryRole.App))
        {
            host.Apply(TelemetryOptions.Default with { Level = TelemetryLevel.ErrorsOnly });
            using var operation = host.StartOperation("zoomin.test.run");
        }

        Assert.DoesNotContain("zoomin.test.run", ReadAll("*.trace.jsonl"), StringComparison.Ordinal);
    }

    [Fact]
    public void CleanUp_DeletesHoursOlderThanTheRetention()
    {
        Directory.CreateDirectory(_folder);
        var now = DateTimeOffset.Now;
        var old = Path.Combine(_folder, $"zoomin-{now.AddHours(-26):yyyyMMdd-HH}.log");
        var recent = Path.Combine(_folder, $"zoomin-{now.AddHours(-2):yyyyMMdd-HH}.log");
        var legacy = Path.Combine(_folder, $"zoomin-{now.AddDays(-3):yyyyMMdd}.log");
        File.WriteAllText(old, "old");
        File.WriteAllText(recent, "recent");
        File.WriteAllText(legacy, "legacy");

        using (Start(TelemetryRole.App))
        {
        }

        Assert.False(File.Exists(old));
        Assert.False(File.Exists(legacy));
        Assert.True(File.Exists(recent));
    }

    private TelemetryHost Start(TelemetryRole role, string? parent = null)
    {
        Activity.Current = null;
        return TelemetryHost.Start(role, parent, _folder, TimeProvider.System);
    }

    private string ReadAll(string pattern) => string.Join(
        Environment.NewLine,
        Directory.Exists(_folder) ? Directory.GetFiles(_folder, pattern).Select(File.ReadAllText) : []);
}
