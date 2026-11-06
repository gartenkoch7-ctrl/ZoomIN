using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Platform.WindowFilters;

namespace ZoomIN.Platform.Tests.WindowFilters;

/// <summary>Which window the shortcut acts on; no screen needed.</summary>
public sealed class TrackerSnapshotTests
{
    private static readonly WindowKind s_word = new("winword.exe", "OpusApp");
    private static readonly byte s_inverted = WindowPalette.For(ColorFilter.Inverted);

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

    private static TrackerSnapshot Snapshot(params (TrackedWindow Window, byte Palette)[] windows) =>
        new([.. windows.Select(w => w.Window)], [.. windows.Select(w => w.Palette)]);

    private static TrackedWindow Window(nint handle, WindowKind kind, WindowRole role, nint owner = 0) =>
        new(handle, new PhysicalRect(0, 0, 400, 300), kind, role, WindowBrightness.Light, owner, 1);
}
