namespace ZoomIN.TrackingProbe.Auto;

/// <summary>How many samples each source answered in.</summary>
internal sealed class SourceHits
{
    public int Samples { get; set; }

    public int System { get; set; }

    public int Msaa { get; set; }

    public int Uia { get; set; }

    public int Focus { get; set; }

    /// <summary>Largest disagreement in px between caret sources that answered at the same time.</summary>
    public double Spread { get; set; }

    /// <summary>Type and name of the last focused element, to explain a missing caret.</summary>
    public string? FocusInfo { get; set; }

    public override string ToString() => Samples == 0
        ? "–"
        : $"S{Share(System)} M{Share(Msaa)} U{Share(Uia)} F{Share(Focus)} Δ{Spread:0}";

    private string Share(int hits) => hits == Samples ? "✓" : hits == 0 ? "✗" : $"{hits}/{Samples}";
}
