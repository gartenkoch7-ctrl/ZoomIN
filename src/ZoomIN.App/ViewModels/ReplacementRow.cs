using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Localization;

namespace ZoomIN.App.ViewModels;

/// <summary>One colour replacement of an own scheme on the scheme page (M18, C3).</summary>
public sealed partial class ReplacementRow : ObservableObject
{
    private readonly Action _changed;
    private readonly ILocalizer _texts;
    private readonly bool _loading;

    public ReplacementRow(int number, ColorReplacement replacement, ILocalizer texts, Action changed)
    {
        _loading = true;
        Number = number;
        _changed = changed;
        _texts = texts;
        From = replacement.From;
        To = replacement.To;
        Tolerance = replacement.Tolerance;
        _loading = false;
    }

    /// <summary>1–3, as the user counts them.</summary>
    public int Number { get; }

    public string FromName => _texts.Format("Schemes.From", Number);

    public string ToName => _texts.Format("Schemes.To", Number);

    public string ToleranceName => _texts.Format("Schemes.Tolerance", Number);

    public string RemoveName => _texts.Format("Schemes.RemoveReplacement", Number);

    [ObservableProperty]
    public partial string From { get; set; }

    [ObservableProperty]
    public partial string To { get; set; }

    [ObservableProperty]
    public partial double Tolerance { get; set; }

    public ColorReplacement ToReplacement() => new() { From = From, To = To, Tolerance = (int)Math.Round(Tolerance) };

    partial void OnFromChanged(string value) => Changed();

    partial void OnToChanged(string value) => Changed();

    partial void OnToleranceChanged(double value) => Changed();

    private void Changed()
    {
        if (!_loading)
        {
            _changed();
        }
    }
}
