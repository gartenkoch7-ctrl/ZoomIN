namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Mean of a fixed number of GPU time samples, so one slow frame does not move a published value.
/// A value type without allocations; shared by <see cref="GpuPassTimer"/> and the smart inversion budget.
/// </summary>
internal struct SampleMean
{
    /// <summary>Samples per published mean (2 s at 60 Hz).</summary>
    public const int Count = 120;

    private double _sum;
    private int _samples;

    /// <summary>Adds one sample; true when it completed a set, with its mean in <paramref name="mean"/>.</summary>
    public bool Add(double value, out double mean)
    {
        _sum += value;
        if (++_samples < Count)
        {
            mean = 0;
            return false;
        }

        mean = _sum / _samples;
        _sum = 0;
        _samples = 0;
        return true;
    }
}
