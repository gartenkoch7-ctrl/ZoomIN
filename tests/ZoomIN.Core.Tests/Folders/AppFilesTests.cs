using ZoomIN.Core.Folders;

namespace ZoomIN.Core.Tests.Folders;

/// <summary>
/// Every ZoomIN file sits in the folder it always did (ADR-0025 moved the paths into one service,
/// existing installations must find their settings, logs and recovery files unchanged).
/// </summary>
public sealed class AppFilesTests
{
    private static readonly IAppFolders s_folders = new Folders(@"C:\L\ZoomIN", @"C:\R\ZoomIN");

    [Fact]
    public void LocalFiles_StayInLocalAppData()
    {
        Assert.Equal(@"C:\L\ZoomIN\logs", s_folders.Logs());
        Assert.Equal(@"C:\L\ZoomIN\diagnostics", s_folders.Diagnostics());
        Assert.Equal(@"C:\L\ZoomIN\debug", s_folders.Debug());
        Assert.Equal(@"C:\L\ZoomIN\updates", s_folders.Updates());
        Assert.Equal(@"C:\L\ZoomIN\setup", s_folders.Setup());
        Assert.Equal(@"C:\L\ZoomIN\crashes.log", s_folders.CrashLog());
        Assert.Equal(@"C:\L\ZoomIN\appbar.hwnd", s_folders.AppBarWindow());
    }

    [Fact]
    public void RoamingFiles_StayInAppData()
    {
        Assert.Equal(@"C:\R\ZoomIN\settings.json", s_folders.Settings());
        Assert.Equal(@"C:\R\ZoomIN\profiles", s_folders.Profiles());
    }

    private sealed record Folders(string Local, string Roaming) : IAppFolders;
}
