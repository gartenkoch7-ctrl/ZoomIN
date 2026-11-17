using System.Security.Cryptography;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>
/// ADR-0031 in the install step: every package must match the published checksum, unsigned releases
/// are signed here, a broken or foreign signature is refused, a failure withdraws the new certificate.
/// </summary>
public sealed class UpdateApplierSigningTests : IDisposable
{
    private readonly UpdateApplierBench _bench = new();

    [Fact]
    public void UnsignedRelease_IsSignedOnThisPc_InTheStagingFolder()
    {
        if (!_bench.CanCreateLinks())
        {
            return;
        }

        _bench.Verifier.Result = PackageCheck.NotSigned;
        var command = _bench.Package("1.0.0");
        _bench.Checksums.Publish(command);

        Assert.Equal(UpdateResult.Installed, _bench.Applier.Apply(_bench.Layout, command));
        var signed = Assert.Single(_bench.Signer.Signed);
        Assert.StartsWith(_bench.Layout.VersionFolder(new AppVersion(1, 0, 0)) + InstallLayout.StagingSuffix + Path.DirectorySeparatorChar, signed, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(InstallLayout.ExecutableName, signed, StringComparison.OrdinalIgnoreCase);

        // Certificates are pruned only after the switch, keeping the installed version's.
        Assert.Contains(_bench.Layout.CurrentExecutable.Replace(InstallLayout.CurrentName, "app-1.0.0", StringComparison.Ordinal), _bench.Signer.Kept);
    }

    [Fact]
    public void SignedButTheMoveFails_WithdrawsTheNewCertificate()
    {
        _bench.Verifier.Result = PackageCheck.NotSigned;

        // A file held open in the target folder (an old instance still running) blocks the move.
        var target = Directory.CreateDirectory(_bench.Layout.VersionFolder(new AppVersion(1, 0, 0))).FullName;
        using (File.Create(Path.Combine(target, "locked.dll"), 1, FileOptions.None))
        {
            Assert.Equal(UpdateResult.Failed, _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.0")));
        }

        // The folder in the way is moved aside, never half deleted.
        Assert.True(File.Exists(Path.Combine(target, "locked.dll")));
        Assert.Single(_bench.Signer.Signed);
        Assert.Equal(1, _bench.Signer.Prunes);
        Assert.DoesNotContain(_bench.Signer.Kept, path => path.Contains(".tmp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnsignedPackageNotPublished_IsNotInstalled_EvenWithAMatchingHashOnTheCommandLine()
    {
        // A non-elevated caller brings its own package and its own hash (security review 2026-11-17).
        _bench.Verifier.Result = PackageCheck.NotSigned;
        var package = _bench.Package("9.9.9");
        _bench.Checksums.Unpublish(package.Version);

        Assert.Equal(UpdateResult.Failed, _bench.Applier.Apply(_bench.Layout, package));
        Assert.Empty(_bench.Signer.Signed);
        Assert.Empty(_bench.Folders());
    }

    [Fact]
    public void SignedPackageNotPublished_IsNotInstalledEither()
    {
        // An installed, locally signed ZoomIN.exe packed next to someone else's DLLs (review 2026-11-17).
        var package = _bench.Package("9.9.9");
        _bench.Checksums.Unpublish(package.Version);

        Assert.Equal(UpdateResult.Failed, _bench.Applier.Apply(_bench.Layout, package));
        Assert.Empty(_bench.Folders());
    }

    [Fact]
    public void UnsignedPackageDifferentFromThePublishedOne_IsRefused()
    {
        _bench.Verifier.Result = PackageCheck.NotSigned;
        var package = _bench.Package("1.0.0");
        _bench.Checksums.Publish(package with { Sha256 = Convert.ToHexStringLower(SHA256.HashData("the real release"u8)) });

        Assert.Equal(UpdateResult.PackageMismatch, _bench.Applier.Apply(_bench.Layout, package));
        Assert.Empty(_bench.Signer.Signed);
    }

    [Fact]
    public void SignedByZoomIn_IsNotSignedAgain()
    {
        if (!_bench.CanCreateLinks())
        {
            return;
        }

        Assert.Equal(UpdateResult.Installed, _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.0")));
        Assert.Empty(_bench.Signer.Signed);
    }

    [Fact]
    public void SigningFails_InstallsNothing()
    {
        _bench.Verifier.Result = PackageCheck.NotSigned;
        _bench.Signer.Succeeds = false;
        var command = _bench.Package("1.0.0");
        _bench.Checksums.Publish(command);

        Assert.Equal(UpdateResult.Failed, _bench.Applier.Apply(_bench.Layout, command));
        Assert.Empty(_bench.Folders());
        Assert.Empty(_bench.Signer.Kept);
    }

    [Theory]
    [InlineData(PackageCheck.SignatureInvalid)]
    [InlineData(PackageCheck.UnexpectedPublisher)]
    public void BrokenOrForeignSignature_IsNeverSignedOver(PackageCheck check)
    {
        _bench.Verifier.Result = check;

        Assert.Equal(UpdateResult.SignatureRejected, _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.0")));
        Assert.Empty(_bench.Signer.Signed);
    }

    public void Dispose() => _bench.Dispose();
}
