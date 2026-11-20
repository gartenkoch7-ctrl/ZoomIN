using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Platform.WindowFilters;

namespace ZoomIN.Platform.Tests.WindowFilters;

/// <summary>Which window the shortcut acts on; no screen needed.</summary>
public sealed class TrackerSnapshotTests
{
    private static readonly WindowKind s_word = new("winword.exe", "OpusApp");
    private static readonly byte s_inverted = WindowPalette.For(FilterId.Inverted);

    [Fact]
    public void PopupInFront_StandsForItsOwner()
    {
        var snapshot = Snapshot(
            (Window(2, new WindowKind("winword.exe", "Net UI Tool Window"), WindowRole.Popup, owner: 1), s_inverted),
            (Window(1, s_word, WindowRole.Normal), s_inverted));

        Assert.True(snapshot.TryFind(2, out var target));
        Assert.Equal(s_word, target.Kind);
    }

    [Fact]
    public void PopupWithoutAListedOwner_GivesNothing()
    {
        var snapshot = Snapshot((Window(2, new WindowKind("winword.exe", "#32768"), WindowRole.Popup, owner: 0), s_inverted));

        Assert.False(snapshot.TryFind(2, out _));
    }

    [Fact]
    public void UnreadableProgramAndWindowsInterface_GiveNothing_AndAreNotOffered()
    {
        var snapshot = Snapshot(
            (Window(1, new WindowKind(WindowKind.UnknownProcess, "#32770"), WindowRole.Normal), WindowPalette.Monitor),
            (Window(2, new WindowKind("explorer.exe", "Shell_TrayWnd"), WindowRole.Shell), WindowPalette.Monitor),
            (Window(3, s_word, WindowRole.Normal), s_inverted));

        Assert.False(snapshot.TryFind(1, out _));
        Assert.False(snapshot.TryFind(2, out _));
        Assert.True(snapshot.TryFind(3, out _));
        Assert.Equal([s_word], snapshot.Kinds());
    }

    [Fact]
    public void AtAPoint_TheFrontMostWindowCounts_AndThePopupStandsForItsOwner()
    {
        var excel = new WindowKind("excel.exe", "XLMAIN");
        var snapshot = Snapshot(
            (Window(3, new WindowKind("excel.exe", "#32768"), WindowRole.Popup, owner: 2, x: 50), s_inverted),
            (Window(2, excel, WindowRole.Normal, x: 0), s_inverted),
            (Window(1, s_word, WindowRole.Normal, x: 0), s_inverted));

        Assert.True(snapshot.TryFindAt(new PhysicalPoint(10, 10), out var front));
        Assert.Equal(excel, front.Kind);
        Assert.True(snapshot.TryFindAt(new PhysicalPoint(300, 10), out var popup)); // inside the popup
        Assert.Equal(excel, popup.Kind);
        Assert.False(snapshot.TryFindAt(new PhysicalPoint(1000, 10), out _));
    }

    [Fact]
    public void ActiveWindow_FallsBackToTheMouse_OnlyForTheWindowsInterface()
    {
        var unknown = new WindowKind(WindowKind.UnknownProcess, "#32770");
        var snapshot = Snapshot(
            (Window(3, new WindowKind("explorer.exe", "Progman"), WindowRole.Shell, x: 2000), WindowPalette.Monitor),
            (Window(2, unknown, WindowRole.Normal, x: 1000), WindowPalette.Monitor),
            (Window(1, s_word, WindowRole.Normal), s_inverted));
        var overWord = new PhysicalPoint(10, 10);

        Assert.True(snapshot.TryFindActive(1, overWord, out var active));
        Assert.Equal(s_word, active.Kind);
        Assert.True(snapshot.TryFindActive(3, overWord, out var fromDesktop)); // desktop active: window under the mouse
        Assert.Equal(s_word, fromDesktop.Kind);
        Assert.True(snapshot.TryFindActive(0, overWord, out _)); // nothing active
        Assert.False(snapshot.TryFindActive(2, overWord, out _)); // an admin window is never swapped for Word
        Assert.False(snapshot.TryFindActive(3, null, out _));
    }

    private static TrackerSnapshot Snapshot(params (TrackedWindow Window, byte Palette)[] windows) =>
        new([.. windows.Select(w => w.Window)], [.. windows.Select(w => w.Palette)]);

    private static TrackedWindow Window(nint handle, WindowKind kind, WindowRole role, nint owner = 0, double x = 0) =>
        new(handle, new PhysicalRect(x, 0, 400, 300), kind, role, WindowBrightness.Light, owner, 1);
}
