namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// The measurement when a window opens (ADR-0020): up to three looks in about a second, two of
/// three decide; looks that fail (window covered or not drawn yet) are repeated until 2 s.
/// Immutable; the tracker keeps one per window until it is decided.
/// </summary>
public readonly record struct BrightnessVote(int Light, int Dark, int Failed)
{
    /// <summary>After this the window is decided with what is there, or stays <see cref="WindowBrightness.Unknown"/>.</summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    // The first look waits for the opening animation; the others give slow apps time to draw.
    private static readonly TimeSpan[] s_schedule =
        [TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(1000)];

    private static readonly TimeSpan s_retry = TimeSpan.FromMilliseconds(300);

    public int Looks => Light + Dark + Failed;

    /// <summary>Two equal results decide; until then <see cref="WindowBrightness.Pending"/>.</summary>
    public WindowBrightness Decision =>
        Light >= 2 ? WindowBrightness.Light : Dark >= 2 ? WindowBrightness.Dark : WindowBrightness.Pending;

    /// <summary>Time after the window appeared at which the next look is due.</summary>
    public TimeSpan NextLookAt => Looks < s_schedule.Length
        ? s_schedule[Looks]
        : s_schedule[^1] + (s_retry * (Looks - s_schedule.Length + 1));

    /// <param name="isLight">Result of one look; null if the window could not be seen.</param>
    public BrightnessVote Add(bool? isLight) => isLight switch
    {
        true => this with { Light = Light + 1 },
        false => this with { Dark = Dark + 1 },
        null => this with { Failed = Failed + 1 },
    };

    /// <summary>
    /// The result <paramref name="elapsed"/> after the window appeared: decided, still pending, or,
    /// past the deadline, the single result there is (a tie or nothing is unknown).
    /// </summary>
    public WindowBrightness ResultAfter(TimeSpan elapsed)
    {
        var decision = Decision;
        if (decision != WindowBrightness.Pending || elapsed < Deadline)
        {
            return decision;
        }

        return Light > Dark ? WindowBrightness.Light : Dark > Light ? WindowBrightness.Dark : WindowBrightness.Unknown;
    }
}
