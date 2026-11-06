using Windows.Win32.Foundation;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>What the tracker knows about one window, from its first appearance until it closes.</summary>
internal sealed class WindowState(WindowKind kind, WindowRole role, bool ignored)
{
    private double _titleBarWidth = -1;
    private double _titleBarHeightOf = -1;

    public WindowKind Kind { get; } = kind;

    public WindowRole Role { get; } = role;

    /// <summary>Shadows and the like: never part of the regions.</summary>
    public bool Ignored { get; } = ignored;

    public WindowBrightness Brightness { get; set; } = WindowBrightness.Pending;

    public BrightnessVote Vote { get; set; }

    /// <summary>The title bar, counted in the same looks as the window but on its own.</summary>
    public WindowBrightness TitleBarBrightness { get; private set; } = WindowBrightness.Pending;

    public BrightnessVote TitleBarVote { get; set; }

    /// <summary>Title bar height from the last reading (see <see cref="TitleBarHeightOf"/>).</summary>
    public int TitleBarHeight { get; private set; }

    /// <summary>Stopwatch timestamp of the current round of looks.</summary>
    public long StartedAt { get; private set; }

    public bool Measuring { get; private set; }

    /// <summary>Rounds of looks so far (a new one only for windows that stayed unknown).</summary>
    public int Rounds { get; private set; }

    /// <summary>
    /// The title bar height of a normal window with a caption. Read again only when the size
    /// changes (maximized, other DPI), since the list runs on every move while a window is dragged.
    /// </summary>
    public int TitleBarHeightOf(in RawWindow raw)
    {
        if (!raw.HasCaption || Role != WindowRole.Normal)
        {
            return 0;
        }

        if (raw.Bounds.Width != _titleBarWidth || raw.Bounds.Height != _titleBarHeightOf)
        {
            _titleBarWidth = raw.Bounds.Width;
            _titleBarHeightOf = raw.Bounds.Height;
            TitleBarHeight = TitleBarReader.HeightOf((HWND)raw.Handle, raw.Bounds);
        }

        return TitleBarHeight;
    }

    public void StartLooks(long now)
    {
        Measuring = true;
        StartedAt = now;
        Vote = default;
        TitleBarVote = default;
    }

    public void Finish(WindowBrightness result)
    {
        Brightness = result;

        // Decided together with the window: whatever the title bar showed more often counts.
        TitleBarBrightness = TitleBarVote.ResultAfter(BrightnessVote.Deadline);
        Measuring = false;
        Rounds++;
    }
}
