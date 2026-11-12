using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Localization;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// The editor of one own scheme (M18, C1–C3): kind, colours, strength, replacements and a preview.
/// Every change goes out as a whole scheme at once, so the screen shows it live.
/// </summary>
public sealed partial class SchemeEditor : ObservableObject
{
    private static readonly string[] s_sampleNames =
        ["Color.White", "Color.Black", "Schemes.Sample.Red", "Color.Green", "Schemes.Sample.Blue", "Color.Yellow", "Schemes.Sample.Grey"];

    private readonly Action<CustomScheme> _changed;
    private readonly FilterNames _names;
    private readonly ILocalizer _texts;
    private CustomScheme _scheme;
    private bool _loading = true;

    public SchemeEditor(CustomScheme scheme, FilterNames names, ILocalizer texts, Action<CustomScheme> changed)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        _scheme = scheme;
        _changed = changed;
        _names = names;
        _texts = texts;
        Name = scheme.Name;
        KindIndex = (int)scheme.Kind;
        Color1 = scheme.Color1;
        Color2 = scheme.Color2;
        Hue = scheme.Hue;
        DeficiencyIndex = (int)scheme.Deficiency;
        Strength = scheme.Strength;
        foreach (var replacement in scheme.Replacements)
        {
            AppendRow(replacement);
        }

        foreach (var sample in SchemePreview.Samples)
        {
            Preview.Add(new PreviewSwatch(sample.ToHex()));
        }

        _loading = false;
        UpdatePreview();
    }

    public FilterId Id => _scheme.Id;

    public CustomScheme Scheme => _scheme;

    public string Title => _texts.Format("Schemes.Editing", _names.Of(Id, [_scheme]));

    /// <summary>Shown in the empty name box: the name lists use while the user gives none.</summary>
    public string DefaultName => _names.DefaultName(Id);

    public ObservableCollection<ReplacementRow> Replacements { get; } = [];

    /// <summary>One swatch per <see cref="SchemePreview.Samples"/>, updated in place.</summary>
    public ObservableCollection<PreviewSwatch> Preview { get; } = [];

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial int KindIndex { get; set; }

    [ObservableProperty]
    public partial string Color1 { get; set; }

    [ObservableProperty]
    public partial string Color2 { get; set; }

    [ObservableProperty]
    public partial string Hue { get; set; }

    [ObservableProperty]
    public partial int DeficiencyIndex { get; set; }

    [ObservableProperty]
    public partial double Strength { get; set; }

    public bool IsTwoColors => KindIndex == (int)SchemeKind.TwoColors;

    public bool IsTint => KindIndex == (int)SchemeKind.Tint;

    public bool IsDeficiency => KindIndex == (int)SchemeKind.Deficiency;

    public bool CanAddReplacement => Replacements.Count < CustomScheme.MaxReplacements;

    /// <summary>Appends a replacement (red becomes yellow); the rows above stay as they are.</summary>
    public void AddReplacement()
    {
        if (CanAddReplacement)
        {
            AppendRow(new ColorReplacement());
            Apply();
        }
    }

    public void RemoveReplacement(ReplacementRow row)
    {
        // Rebuilt so the numbers stay 1, 2, 3 after a removal; the page moves the focus.
        var rest = Replacements.Where(r => r != row).Select(r => r.ToReplacement()).ToList();
        Replacements.Clear();
        foreach (var replacement in rest)
        {
            AppendRow(replacement);
        }

        Apply();
    }

    partial void OnNameChanged(string value)
    {
        Apply();
        OnPropertyChanged(nameof(Title));
    }

    partial void OnKindIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsTwoColors));
        OnPropertyChanged(nameof(IsTint));
        OnPropertyChanged(nameof(IsDeficiency));
        Apply();
    }

    partial void OnColor1Changed(string value) => Apply();

    partial void OnColor2Changed(string value) => Apply();

    partial void OnHueChanged(string value) => Apply();

    partial void OnDeficiencyIndexChanged(int value) => Apply();

    partial void OnStrengthChanged(double value) => Apply();

    private void AppendRow(ColorReplacement replacement)
    {
        Replacements.Add(new ReplacementRow(Replacements.Count + 1, replacement, _texts, Apply));
        OnPropertyChanged(nameof(CanAddReplacement));
    }

    private void Apply()
    {
        if (_loading || KindIndex < 0 || DeficiencyIndex < 0)
        {
            return;
        }

        _scheme = CustomSchemes.NormalizedScheme(_scheme with
        {
            Name = Name ?? string.Empty,
            Kind = (SchemeKind)KindIndex,
            Color1 = Color1,
            Color2 = Color2,
            Hue = Hue,
            Deficiency = (ColorDeficiency)DeficiencyIndex,
            Strength = (int)Math.Round(Strength),
            Replacements = [.. Replacements.Select(r => r.ToReplacement())],
        });
        _changed(_scheme);
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var after = SchemePreview.Apply(_scheme, SchemePreview.Samples);
        for (var i = 0; i < Preview.Count; i++)
        {
            var hex = after[i].ToHex();
            Preview[i].After = hex;
            Preview[i].Description = _texts.Format("Schemes.Preview.Item", _texts.Get(s_sampleNames[i]), hex);
        }
    }
}
