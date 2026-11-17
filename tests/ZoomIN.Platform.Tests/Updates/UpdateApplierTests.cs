using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>
/// Installs fake packages into a temp "Program Files\ZoomIN". Directory links need administrator
/// rights or Windows developer mode; without them the link tests return early (CI runs as admin).
/// </summary>
public sealed class UpdateApplierTests : IDisposable
{
    private readonly UpdateApplierBench _bench = new();

    [Fact]
    public void FirstInstall_CreatesVersionFolderAndLink()
    {
        if (!_bench.CanCreateLinks())
        {
            return;
        }

        Assert.Equal(UpdateResult.Installed, _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.0")));

        Assert.Equal(new AppVersion(1, 0, 0), UpdateApplier.ActiveVersion(_bench.Layout));
        Assert.Equal("1.0.0", File.ReadAllText(_bench.Layout.CurrentExecutable));
    }

    [Fact]
    public void Updates_KeepPreviousAndRemoveOlderVersions()
    {
        if (!_bench.CanCreateLinks())
        {
            return;
        }

        _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.0"));
        _bench.Applier.Apply(_bench.Layout, _bench.Package("1.1.0"));
        Assert.Equal(UpdateResult.Installed, _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.1")));

        Assert.Equal("1.0.1", File.ReadAllText(_bench.Layout.CurrentExecutable));
        Assert.Equal(["app-1.0.1", "app-1.1.0", "current"], _bench.Folders());
    }

    [Fact]
    public void ActiveVersion_IsNotInstalledAgain()
    {
        if (!_bench.CanCreateLinks())
        {
            return;
        }

        _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.0"));

        Assert.Equal(UpdateResult.AlreadyActive, _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.0")));
    }

    [Fact]
    public void ActiveVersionNoLongerTrusted_IsInstalledAgain_AndKeepsThePreviousVersion()
    {
        if (!_bench.CanCreateLinks())
        {
            return;
        }

        _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.0"));
        _bench.Applier.Apply(_bench.Layout, _bench.Package("1.1.0"));
        _bench.Verifier.Result = PackageCheck.NotSigned;

        Assert.Equal(UpdateResult.Installed, _bench.Applier.Apply(_bench.Layout, _bench.Package("1.1.0")));
        Assert.Equal(["app-1.0.0", "app-1.1.0", "current"], _bench.Folders());
    }

    [Fact]
    public void WrongHash_ChangesNothing()
    {
        var command = _bench.Package("1.0.0") with { Sha256 = new string('0', 64) };

        Assert.Equal(UpdateResult.PackageMismatch, _bench.Applier.Apply(_bench.Layout, command));
        Assert.Empty(_bench.Folders());
        Assert.Empty(Directory.GetFiles(_bench.Layout.Root));
    }

    [Fact]
    public void RejectedSignature_ChangesNothing()
    {
        _bench.Verifier.Result = PackageCheck.UnexpectedPublisher;

        Assert.Equal(UpdateResult.SignatureRejected, _bench.Applier.Apply(_bench.Layout, _bench.Package("1.0.0")));
        Assert.Empty(_bench.Folders());
    }

    [Fact]
    public void MissingPackage_Fails()
    {
        var command = _bench.Package("1.0.0") with { PackagePath = Path.Combine(_bench.Temp, "gone.zip") };

        Assert.Equal(UpdateResult.Failed, _bench.Applier.Apply(_bench.Layout, command));
        Assert.Empty(_bench.Folders());
    }

    public void Dispose() => _bench.Dispose();
}
