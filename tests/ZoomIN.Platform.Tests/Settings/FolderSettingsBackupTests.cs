using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Settings;

namespace ZoomIN.Platform.Tests.Settings;

/// <summary>Works on a private temp folder instead of %AppData%\ZoomIN.</summary>
public sealed class FolderSettingsBackupTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ZoomIN.Tests.").FullName;

    [Fact]
    public void SettingsAndProfiles_AreCopied()
    {
        File.WriteAllText(Path.Combine(_root, "settings.json"), "{}");
        var profiles = Directory.CreateDirectory(Path.Combine(_root, "profiles")).FullName;
        File.WriteAllText(Path.Combine(profiles, "Lesen.json"), "{}");

        var backup = new FolderSettingsBackup(NullLog.Instance, _root).Backup(new AppVersion(1, 1, 0));

        Assert.NotNull(backup);
        Assert.StartsWith(Path.Combine(_root, "backups", "1.1.0-"), backup, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(backup, "settings.json")));
        Assert.True(File.Exists(Path.Combine(backup, "profiles", "Lesen.json")));
    }

    [Fact]
    public void NothingToBackUp_ReturnsNull()
    {
        Assert.Null(new FolderSettingsBackup(NullLog.Instance, _root).Backup(new AppVersion(1, 0, 0)));
        Assert.False(Directory.Exists(Path.Combine(_root, "backups")));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
