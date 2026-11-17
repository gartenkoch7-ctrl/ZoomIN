using ZoomIN.Core.Updates;

namespace ZoomIN.Core.Tests.Updates;

public sealed class InstallLayoutTests
{
    [Theory]
    [InlineData(@"C:\Program Files\ZoomIN\current\ZoomIN.exe")]
    [InlineData(@"C:\Program Files\ZoomIN\app-1.1.0-dev.42\ZoomIN.exe")]
    [InlineData(@"C:\Program Files\ZoomIN\CURRENT\ZoomIN.exe")]
    public void VersionedInstall_IsRecognised(string exe)
    {
        Assert.True(InstallLayout.TryFromExecutable(exe, out var layout));
        Assert.Equal(@"C:\Program Files\ZoomIN", layout.Root);
        Assert.Equal(@"C:\Program Files\ZoomIN\current\ZoomIN.exe", layout.CurrentExecutable);
    }

    [Theory]
    [InlineData(@"C:\Program Files\ZoomIN\ZoomIN.exe")]
    [InlineData(@"D:\Projekte\ZoomIN\src\ZoomIN.App\bin\Debug\win-x64\ZoomIN.exe")]
    [InlineData(@"C:\Tools\app-latest\ZoomIN.exe")]
    [InlineData(@"ZoomIN.exe")]
    [InlineData(null)]
    public void OtherLocations_CannotUpdate(string? exe)
    {
        Assert.False(InstallLayout.TryFromExecutable(exe, out _));
    }

    [Theory]
    [InlineData(@"C:\Program Files\ZoomIN\current\ZoomIN.exe", true)]
    [InlineData(@"C:\PROGRAM FILES\zoomin\app-1.0.0\ZoomIN.exe", true)]
    [InlineData(@"C:\Users\x\AppData\Local\Temp\x\app-1.0.0\ZoomIN.exe", false)]
    [InlineData(@"C:\Program Files\ZoomIN2\current\ZoomIN.exe", false)]
    [InlineData(@"C:\Program Files\ZoomIN\..\Evil\current\ZoomIN.exe", false)]
    public void OnlyTheProgramFilesInstall_IsTheInstallation(string exe, bool expected)
    {
        Assert.True(InstallLayout.TryFromExecutable(exe, out var layout));

        Assert.Equal(expected, layout.IsAt(@"C:\Program Files\ZoomIN\"));
    }

    [Fact]
    public void VersionFolder_IsNamedAfterVersion()
    {
        var layout = new InstallLayout(@"C:\Program Files\ZoomIN");

        Assert.Equal(@"C:\Program Files\ZoomIN\app-1.0.0-rc1", layout.VersionFolder(new AppVersion(1, 0, 0, "rc1")));
    }

    [Fact]
    public void Cleanup_KeepsActiveAndPreviousVersion()
    {
        string[] folders = ["app-1.0.0", "app-1.0.1", "app-1.1.0", "app-1.2.0.tmp", "current", "logs"];

        var remove = InstallLayout.FoldersToRemove(folders, new AppVersion(1, 1, 0), new AppVersion(1, 0, 1));

        Assert.Equal(["app-1.0.0", "app-1.2.0.tmp"], remove);
    }

    [Theory]
    [InlineData("app-1.1.0-dev.42.tmp")]
    [InlineData("app-1.0.0-rc1.old.tmp")]
    [InlineData("app-1.0.0.TMP")]
    public void StagingAndSetAsideFolders_AreNoVersions(string name)
    {
        Assert.False(InstallLayout.TryParseFolder(name, out _));
        Assert.Equal([name], InstallLayout.FoldersToRemove([name], new AppVersion(1, 1, 0), previous: null));
    }

    [Fact]
    public void Cleanup_WithoutPreviousVersion_KeepsOnlyActive()
    {
        var remove = InstallLayout.FoldersToRemove(["app-1.0.0", "app-1.1.0"], new AppVersion(1, 1, 0), previous: null);

        Assert.Equal(["app-1.0.0"], remove);
    }
}
