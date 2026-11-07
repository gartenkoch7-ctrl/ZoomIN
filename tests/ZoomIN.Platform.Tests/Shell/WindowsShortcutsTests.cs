using ZoomIN.Platform.Shell;

namespace ZoomIN.Platform.Tests.Shell;

/// <summary>The shortcut service creates and removes a real .lnk file (in a test folder, never on the desktop).</summary>
public sealed class WindowsShortcutsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ZoomIN-Shortcuts-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void CreateAndRemove_ALink()
    {
        var shortcuts = new WindowsShortcuts();
        var link = Path.Combine(_folder, WindowsShortcuts.FileName);

        shortcuts.Create(link, Environment.ProcessPath!);

        Assert.True(File.Exists(link));

        shortcuts.Remove(link);
        shortcuts.Remove(link); // a second time: nothing to remove, no error

        Assert.False(File.Exists(link));
    }

    [Fact]
    public void KnownPlaces_EndInTheZoomInLink()
    {
        var shortcuts = new WindowsShortcuts();

        Assert.EndsWith(WindowsShortcuts.FileName, shortcuts.CommonStartMenu, StringComparison.Ordinal);
        Assert.EndsWith(WindowsShortcuts.FileName, shortcuts.UserDesktop, StringComparison.Ordinal);
    }
}
