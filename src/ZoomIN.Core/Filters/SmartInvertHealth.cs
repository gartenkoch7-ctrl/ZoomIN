namespace ZoomIN.Core.Filters;

/// <summary>
/// What the diagnostics page says about the smart inversion on one monitor: a fall-back to "keep
/// hue" because the GPU lacks a feature (ADR-0024 decision 6), or only a note that it is slower
/// than its budget while it keeps running (ADR-0032).
/// </summary>
/// <param name="Monitor">Device name of the monitor.</param>
/// <param name="Reason"><see cref="GpuFeature"/> or <see cref="Slow"/>.</param>
/// <param name="Milliseconds">Measured mean extra GPU time for <see cref="Slow"/>, else 0.</param>
public sealed record SmartInvertFallback(string Monitor, string Reason, double Milliseconds)
{
    /// <summary>The weights could not be set up: the monitor shows "keep hue".</summary>
    public const string GpuFeature = "gpu_feature";

    /// <summary>Over the budget (SmartBudget): only reported, the monitor stays smart (ADR-0032).</summary>
    public const string Slow = "budget";

    /// <summary>The monitor shows "keep hue" instead of the smart inversion.</summary>
    public bool ShowsKeepHue => Reason != Slow;
}

/// <summary>
/// Whether the smart inversion runs as intended. Render threads report a fall-back or a slow monitor once per
/// pipeline; the diagnostics page reads <see cref="Fallbacks"/>. Lock-free for readers.
/// </summary>
public sealed class SmartInvertHealth
{
    private IReadOnlyList<SmartInvertFallback> _fallbacks = [];

    public IReadOnlyList<SmartInvertFallback> Fallbacks => Volatile.Read(ref _fallbacks);

    /// <summary>Records the fall-back of one monitor; a newer one replaces the monitor's old entry.</summary>
    public void Report(SmartInvertFallback fallback)
    {
        IReadOnlyList<SmartInvertFallback> current, next;
        do
        {
            current = Volatile.Read(ref _fallbacks);
            next = [.. current.Where(f => f.Monitor != fallback.Monitor), fallback];
        }
        while (Interlocked.CompareExchange(ref _fallbacks, next, current) != current);
    }
}
