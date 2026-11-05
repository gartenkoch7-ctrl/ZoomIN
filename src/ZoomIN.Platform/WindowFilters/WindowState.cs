using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>What the tracker knows about one window, from its first appearance until it closes.</summary>
internal sealed class WindowState(WindowKind kind, WindowRole role, bool ignored)
{
    public WindowKind Kind { get; } = kind;

    public WindowRole Role { get; } = role;

    /// <summary>Shadows and the like: never part of the regions.</summary>
    public bool Ignored { get; } = ignored;

    public WindowBrightness Brightness { get; set; } = WindowBrightness.Pending;

    public BrightnessVote Vote { get; set; }

    /// <summary>Stopwatch timestamp of the current round of looks.</summary>
    public long StartedAt { get; private set; }

    public bool Measuring { get; private set; }

    /// <summary>Rounds of looks so far (a new one only for windows that stayed unknown).</summary>
    public int Rounds { get; private set; }

    public void StartLooks(long now)
    {
        Measuring = true;
        StartedAt = now;
        Vote = default;
    }

    public void Finish(WindowBrightness result)
    {
        Brightness = result;
        Measuring = false;
        Rounds++;
    }
}
