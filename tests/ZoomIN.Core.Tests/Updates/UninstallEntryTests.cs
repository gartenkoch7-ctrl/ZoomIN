using ZoomIN.Core.Updates;

namespace ZoomIN.Core.Tests.Updates;

public sealed class UninstallEntryTests
{
    [Fact]
    public void Entry_PointsAtCurrentLink()
    {
        var entry = UninstallEntry.For(new InstallLayout(@"C:\Program Files\ZoomIN"), new AppVersion(1, 1, 0, "dev.4"));

        Assert.Equal("1.1.0-dev.4", entry.DisplayVersion);
        Assert.Equal(@"C:\Program Files\ZoomIN", entry.InstallLocation);
        Assert.Equal(@"C:\Program Files\ZoomIN\current\ZoomIN.exe", entry.DisplayIcon);
        Assert.Equal(@"""C:\Program Files\ZoomIN\current\ZoomIN.exe"" --uninstall", entry.UninstallString);
    }

    [Theory]
    [InlineData(@"""C:\Users\x\AppData\Local\Programs\ZoomIN\unins000.exe""", @"C:\Users\x\AppData\Local\Programs\ZoomIN\unins000.exe")]
    [InlineData(@"""C:\My Apps\unins000.exe"" /LOG", @"C:\My Apps\unins000.exe")]
    [InlineData(@"C:\ZoomIN\unins000.exe /SILENT", @"C:\ZoomIN\unins000.exe")]
    [InlineData(@"C:\ZoomIN\unins000.exe", @"C:\ZoomIN\unins000.exe")]
    [InlineData(@"""", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void InnoUninstaller_IsExtracted(string? uninstallString, string? expected)
    {
        Assert.Equal(expected, LegacyInstall.UninstallerPath(uninstallString));
    }
}
