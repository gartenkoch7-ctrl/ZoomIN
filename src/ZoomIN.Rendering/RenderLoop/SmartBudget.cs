namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// When the smart inversion is too dear (ADR-0024, decision 6, revised 2026-11-17): only when it
/// takes a real share of the frame time, several measured means in a row. A fixed 1 ms failed on a
/// fast GPU at idle clocks (1.68 ms measured live, 0.05–0.2 ms offline): the GPU was idle, so the
/// time said nothing about lost frames. A value type without allocations (AGENTS.md 2.3).
/// </summary>
internal struct SmartBudget
{
    /// <summary>Share of the monitor's frame time the smart inversion may take.</summary>
    public const double FrameShare = 0.2;

    /// <summary>Means over the budget in a row before falling back (each SampleMean.Count frames).</summary>
    public const int Strikes = 3;

    private int _over;

    public SmartBudget(double refreshHz) => Milliseconds = For(refreshHz);

    /// <summary>Extra GPU time per frame the smart inversion may take on this monitor.</summary>
    public double Milliseconds { get; }

    /// <summary><see cref="FrameShare"/> of one frame; an unknown rate counts as 60 Hz.</summary>
    public static double For(double refreshHz) =>
        FrameShare * 1000 / (refreshHz is > 0 and < 1000 ? refreshHz : 60);

    /// <summary>Starts counting from zero, e.g. after the magnify extra is measured again.</summary>
    public void Reset() => _over = 0;

    /// <summary>Counts one mean; true once <see cref="Strikes"/> means in a row were over the budget.</summary>
    public bool Exceeded(double meanMilliseconds)
    {
        _over = meanMilliseconds > Milliseconds ? _over + 1 : 0;
        return _over >= Strikes;
    }
}
