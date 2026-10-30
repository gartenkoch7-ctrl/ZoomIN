namespace ZoomIN.Core.Filters;

/// <summary>Colour filter selection plus contrast/brightness (UI for the latter in M7).</summary>
public sealed record FilterSettings(bool Enabled, ColorFilter Filter, double Contrast, double Brightness)
{
    public static FilterSettings Default { get; } = new(false, ColorFilter.Inverted, 1.0, 0.0);

    /// <summary>True when the colours differ from the original, i.e. an overlay is needed even at 1×.</summary>
    public bool ChangesColors => Enabled || Contrast != 1.0 || Brightness != 0.0;

    public FilterSettings Normalized() => this with
    {
        Contrast = Math.Clamp(Contrast, 0.5, 3.0),
        Brightness = Math.Clamp(Brightness, -0.5, 0.5),
    };
}
