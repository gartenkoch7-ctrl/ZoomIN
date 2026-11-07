using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Updates;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.App.Tests.Updates;

/// <summary>
/// The elevated update step (ADR-0015) against a temp "Program Files\ZoomIN" with fake Apps entry,
/// secure desktop and signature (M14 step 11). Switching the "current" link needs administrator
/// rights or developer mode; those tests are skipped without them.
/// </summary>
public sealed class UpdateRunnerTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("ZoomIN-UpdateRunnerTests-").FullName;
    private readonly UninstallFakes _fakes = new();
    private readonly UninstallFakes.Registration _registration;
    private readonly FakeVerifier _verifier = new();

    public UpdateRunnerTests()
    {
        _fakes.Layout = new InstallLayout(Directory.CreateDirectory(Path.Combine(_temp, "ZoomIN")).FullName);
        _registration = new UninstallFakes.Registration(_fakes);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void NotInstalled_ChangesNothing()
    {
        _fakes.Layout = null;

        Assert.Equal((int)UpdateResult.NotInstalled, Runner().Run(Package("1.2.0")));
        Assert.Empty(_fakes.Calls);
    }

    [Fact]
    public void RejectedSignature_LeavesAppsEntryAndSecureDesktopAlone()
    {
        _verifier.Result = PackageCheck.UnexpectedPublisher;

        Assert.Equal((int)UpdateResult.SignatureRejected, Runner().Run(Package("1.2.0")));
        Assert.Empty(_fakes.Calls);
    }

    [Fact]
    public void Installed_UpdatesAppsEntryAndSecureDesktop()
    {
        Assert.SkipUnless(CanCreateLinks(), "Directory links need administrator rights or developer mode.");

        Assert.Equal((int)UpdateResult.Installed, Runner().Run(Package("1.2.0") with { SecureDesktop = true }));
        Assert.Equal(["register 1.2.0", "secure-apply True"], _fakes.Calls);
        Assert.Equal(new AppVersion(1, 2, 0), UpdateApplier.ActiveVersion(_fakes.Layout!));
    }

    [Fact]
    public void FailingAppsEntry_StillCountsAsInstalled()
    {
        Assert.SkipUnless(CanCreateLinks(), "Directory links need administrator rights or developer mode.");
        _registration.RegisterFails = new UnauthorizedAccessException("registry");

        Assert.Equal((int)UpdateResult.Installed, Runner().Run(Package("1.2.0")));
        Assert.Equal(["register 1.2.0", "secure-apply keep"], _fakes.Calls);
    }

    private UpdateRunner Runner() => new(
        _verifier, _registration, new UninstallFakes.SecureDesktop(_fakes), new UninstallFakes.Install(_fakes),
        NullLogger<UpdateRunner>.Instance);

    private UpdateCommand Package(string version)
    {
        Assert.True(AppVersion.TryParse(version, out var parsed));
        var source = Directory.CreateDirectory(Path.Combine(_temp, "src-" + version)).FullName;
        File.WriteAllText(Path.Combine(source, InstallLayout.ExecutableName), version);
        var zip = Path.Combine(_temp, $"package-{version}.zip");
        File.Delete(zip);
        ZipFile.CreateFromDirectory(source, zip);
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
        return new UpdateCommand(zip, hash, parsed);
    }

    private bool CanCreateLinks()
    {
        var probe = Path.Combine(_temp, "probe");
        try
        {
            Directory.CreateSymbolicLink(probe, _temp);
            Directory.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed class FakeVerifier : ISignatureVerifier
    {
        public PackageCheck Result { get; set; } = PackageCheck.Ok;

        public PackageCheck Verify(string executablePath) => File.Exists(executablePath) ? Result : PackageCheck.NotSigned;
    }
}
