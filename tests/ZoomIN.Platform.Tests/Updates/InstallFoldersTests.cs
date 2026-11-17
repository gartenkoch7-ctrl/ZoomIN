using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>A version folder that is replaced is never lost when the new one cannot take its place.</summary>
public sealed class InstallFoldersTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("ZoomIN.Tests.").FullName;

    [Fact]
    public void NewFolderCannotMoveIn_TheReplacedFolderIsPutBack()
    {
        var layout = new InstallLayout(_temp);
        var target = layout.VersionFolder(new AppVersion(1, 0, 0));
        var staging = target + InstallLayout.StagingSuffix;
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "ZoomIN.exe"), "active");
        Directory.CreateDirectory(staging);

        // A file held open in the new folder (a virus scanner reading it) blocks its move.
        using (File.Create(Path.Combine(staging, "locked.dll"), 1, FileOptions.None))
        {
            Assert.ThrowsAny<IOException>(() => new InstallFolders(NullLogger.Instance).MoveIntoPlace(layout, staging, target));
        }

        Assert.Equal("active", File.ReadAllText(Path.Combine(target, "ZoomIN.exe")));
        Assert.False(Directory.Exists(target + ".old" + InstallLayout.StagingSuffix));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);
}
