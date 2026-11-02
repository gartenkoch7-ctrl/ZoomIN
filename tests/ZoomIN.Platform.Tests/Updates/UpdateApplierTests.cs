using System.IO.Compression;
using System.Security.Cryptography;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>
/// Installs fake packages into a temp "Program Files\ZoomIN". Directory links need administrator
/// rights or Windows developer mode; without them the link tests return early (CI runs as admin).
/// </summary>
public sealed class UpdateApplierTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("ZoomIN.Tests.").FullName;
    private readonly InstallLayout _layout;
    private readonly FakeVerifier _verifier = new();
    private readonly UpdateApplier _applier;

    public UpdateApplierTests()
    {
        _layout = new InstallLayout(Directory.CreateDirectory(Path.Combine(_temp, "ZoomIN")).FullName);
        _applier = new UpdateApplier(_verifier, NullLog.Instance);
    }

    [Fact]
    public void FirstInstall_CreatesVersionFolderAndLink()
    {
        if (!CanCreateLinks())
        {
            return;
        }

        Assert.Equal(UpdateResult.Installed, _applier.Apply(_layout, Package("1.0.0")));

        Assert.Equal(new AppVersion(1, 0, 0), UpdateApplier.ActiveVersion(_layout));
        Assert.Equal("1.0.0", File.ReadAllText(_layout.CurrentExecutable));
    }

    [Fact]
    public void Updates_KeepPreviousAndRemoveOlderVersions()
    {
        if (!CanCreateLinks())
        {
            return;
        }

        _applier.Apply(_layout, Package("1.0.0"));
        _applier.Apply(_layout, Package("1.1.0"));
        Assert.Equal(UpdateResult.Installed, _applier.Apply(_layout, Package("1.0.1")));

        Assert.Equal("1.0.1", File.ReadAllText(_layout.CurrentExecutable));
        Assert.Equal(["app-1.0.1", "app-1.1.0", "current"], Folders());
    }

    [Fact]
    public void ActiveVersion_IsNotInstalledAgain()
    {
        if (!CanCreateLinks())
        {
            return;
        }

        _applier.Apply(_layout, Package("1.0.0"));

        Assert.Equal(UpdateResult.AlreadyActive, _applier.Apply(_layout, Package("1.0.0")));
    }

    [Fact]
    public void WrongHash_ChangesNothing()
    {
        var command = Package("1.0.0") with { Sha256 = new string('0', 64) };

        Assert.Equal(UpdateResult.PackageMismatch, _applier.Apply(_layout, command));
        Assert.Empty(Folders());
        Assert.Empty(Directory.GetFiles(_layout.Root));
    }

    [Fact]
    public void RejectedSignature_ChangesNothing()
    {
        _verifier.Result = PackageCheck.UnexpectedPublisher;

        Assert.Equal(UpdateResult.SignatureRejected, _applier.Apply(_layout, Package("1.0.0")));
        Assert.Empty(Folders());
    }

    [Fact]
    public void MissingPackage_Fails()
    {
        var command = Package("1.0.0") with { PackagePath = Path.Combine(_temp, "gone.zip") };

        Assert.Equal(UpdateResult.Failed, _applier.Apply(_layout, command));
        Assert.Empty(Folders());
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

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

    private string[] Folders() =>
        [.. Directory.GetDirectories(_layout.Root).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];

    private bool CanCreateLinks()
    {
        try
        {
            var probe = Path.Combine(_temp, "probe");
            Directory.CreateSymbolicLink(probe, _temp);
            Directory.Delete(probe);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
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
