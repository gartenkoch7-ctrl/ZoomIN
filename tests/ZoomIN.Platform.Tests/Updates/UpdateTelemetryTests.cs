using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>An installation is one operation with its signature check inside, both with their result.</summary>
public sealed class UpdateTelemetryTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("ZoomIN.Tests.").FullName;
    private readonly ConcurrentBag<Activity> _finished = [];
    private readonly ActivityListener _listener;

    public UpdateTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Telemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _finished.Add,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        Directory.Delete(_temp, recursive: true);
    }

    [Fact]
    public void RejectedSignature_MarksApplyAndVerifyFailed()
    {
        var applier = new UpdateApplier(new Verifier(PackageCheck.UnexpectedPublisher), NullLogger.Instance);
        var layout = new InstallLayout(Directory.CreateDirectory(Path.Combine(_temp, "ZoomIN")).FullName);

        // Own root, so operations of tests running in parallel are not mixed in.
        using (var root = Telemetry.Source.StartActivity("zoomin.test.update"))
        {
            Assert.Equal(UpdateResult.SignatureRejected, applier.Apply(layout, Package("2.0.0")));
            var trace = root!.TraceId;

            var apply = Single("zoomin.update.apply", trace);
            var verify = Single("zoomin.update.verify", trace);
            Assert.Equal(root.SpanId, apply.ParentSpanId);
            Assert.Equal(apply.SpanId, verify.ParentSpanId);
            Assert.Equal("2.0.0", apply.GetTagItem(UpdateTelemetry.VersionTag));
            Assert.Equal(nameof(UpdateResult.SignatureRejected), apply.GetTagItem(UpdateTelemetry.ResultTag));
            Assert.Equal(ActivityStatusCode.Error, apply.Status);
            Assert.Equal(nameof(PackageCheck.UnexpectedPublisher), verify.GetTagItem(UpdateTelemetry.ResultTag));
            Assert.Equal(ActivityStatusCode.Error, verify.Status);
        }
    }

    [Fact]
    public void ActiveVersion_IsASuccessfulOperation()
    {
        var applier = new UpdateApplier(new Verifier(PackageCheck.Ok), NullLogger.Instance);
        var layout = new InstallLayout(Directory.CreateDirectory(Path.Combine(_temp, "ZoomIN")).FullName);
        Directory.CreateDirectory(layout.VersionFolder(new AppVersion(1, 0, 0)));

        using var root = Telemetry.Source.StartActivity("zoomin.test.update");
        var result = applier.Apply(layout, Package("1.0.0"));

        var apply = Single("zoomin.update.apply", root!.TraceId);
        Assert.Equal(result.ToString(), apply.GetTagItem(UpdateTelemetry.ResultTag));
        Assert.Equal(UpdateTelemetry.Succeeded(result) ? ActivityStatusCode.Unset : ActivityStatusCode.Error, apply.Status);
    }

    private Activity Single(string name, ActivityTraceId trace) =>
        Assert.Single(_finished, activity => activity.DisplayName == name && activity.TraceId == trace);

    private UpdateCommand Package(string version)
    {
        Assert.True(AppVersion.TryParse(version, out var parsed));
        var source = Directory.CreateDirectory(Path.Combine(_temp, "src-" + version)).FullName;
        File.WriteAllText(Path.Combine(source, InstallLayout.ExecutableName), version);
        var zip = Path.Combine(_temp, $"package-{version}.zip");
        ZipFile.CreateFromDirectory(source, zip);
        return new UpdateCommand(zip, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip))), parsed);
    }

    private sealed class Verifier(PackageCheck result) : ISignatureVerifier
    {
        public PackageCheck Verify(string executablePath) => result;
    }
}
