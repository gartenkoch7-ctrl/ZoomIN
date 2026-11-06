namespace ZoomIN.Core.Filters;

/// <summary>Why a monitor shows "keep hue" instead of the smart inversion (ADR-0024 decision 6).</summary>
/// <param name="Monitor">Device name of the monitor.</param>
/// <param name="Reason">"gpu_feature" (the weights could not be set up) or "budget" (too slow).</param>
/// <param name="Milliseconds">Measured mean GPU time of the ambient pass for "budget", else 0.</param>
public sealed record SmartInvertFallback(string Monitor, string Reason, double Milliseconds);

/// <summary>
/// Whether the smart inversion runs as intended. Render threads report a fall-back once per
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
