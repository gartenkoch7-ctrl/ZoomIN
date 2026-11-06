using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Views;
using ZoomIN.Core.Filters;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Detail page of the filter per window (ADR-0020): on/off, automatic filter, sensitivity and the
/// own settings per app + window class. Applies live; the settings service saves.
/// </summary>
public sealed partial class WindowFilterViewModel : ObservableObject
{
    private readonly PageContext _context;
    private readonly IReadOnlyList<string> _choices = WindowRuleRow.AllChoices();
    private readonly IReadOnlyList<string> _titleBarChoices = WindowRuleRow.AllTitleBarChoices();
    private List<WindowKind> _addable = [];
    private bool _loading = true;

    public WindowFilterViewModel(PageContext context)
    {
        _context = context;
        var options = Model.Options;
        FilterNames = Enum.GetValues<ColorFilter>().Select(f => Text.Get("Filter." + f)).ToList();
        SensitivityNames = [Text.Get("WinFilter.Low"), Text.Get("WinFilter.Medium"), Text.Get("WinFilter.High"), Text.Get("WinFilter.Custom")];
        Enabled = options.Enabled;
        AutoFilterIndex = (int)options.AutoFilter;
        SensitivityIndex = (int)options.Sensitivity;
        Percent = options.CustomPercent;
        LoadRules();
        LoadAddable();
        _loading = false;
    }

    public IReadOnlyList<string> FilterNames { get; }

    public IReadOnlyList<string> SensitivityNames { get; }

    public ObservableCollection<WindowRuleRow> Rules { get; } = [];

    public ObservableCollection<string> AddableNames { get; } = [];

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial int AutoFilterIndex { get; set; }

    [ObservableProperty]
    public partial int SensitivityIndex { get; set; }

    [ObservableProperty]
    public partial double Percent { get; set; }

    [ObservableProperty]
    public partial int AddIndex { get; set; } = -1;

    [ObservableProperty]
    public partial bool HasRules { get; set; }

    [ObservableProperty]
    public partial bool CanAdd { get; set; }

    public bool IsCustom => SensitivityIndex == (int)WindowFilterSensitivity.Custom;

    public bool NoRules => !HasRules;

    private WindowFilterModel Model => _context.Controller.Windows;

    public void Add()
    {
        if (AddIndex < 0 || AddIndex >= _addable.Count)
        {
            return;
        }

        // Starts on "automatic" (what it has now); the user picks the filter in the list.
        Change(o => o.WithRule(WindowFilterRule.For(_addable[AddIndex], WindowFilterChoice.Automatic)));
        LoadRules();
        LoadAddable();
    }

    public void Remove(WindowRuleRow row)
    {
        Change(o => o.WithoutRule(row.Kind));
        Rules.Remove(row);
        HasRules = Rules.Count > 0;
        LoadAddable();
    }

    public void ResetAll()
    {
        Change(o => o with { Rules = [] });
        LoadRules();
        LoadAddable();
    }

    partial void OnEnabledChanged(bool value) => Change(o => o with { Enabled = value });

    partial void OnAutoFilterIndexChanged(int value)
    {
        if (value >= 0)
        {
            Change(o => o with { AutoFilter = (ColorFilter)value });
        }
    }

    partial void OnSensitivityIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustom));
        if (value >= 0)
        {
            Change(o => o with { Sensitivity = (WindowFilterSensitivity)value });
        }
    }

    partial void OnPercentChanged(double value) => Change(o => o with { CustomPercent = (int)Math.Round(value) });

    partial void OnAddIndexChanged(int value) => CanAdd = value >= 0;

    partial void OnHasRulesChanged(bool value) => OnPropertyChanged(nameof(NoRules));

    private void Change(Func<WindowFilterOptions, WindowFilterOptions> change)
    {
        if (!_loading)
        {
            Model.SetOptions(change(Model.Options));
        }
    }

    // Keeps the row order of the settings (newest first).
    private void OnRuleChanged(WindowRuleRow row)
    {
        var rule = row.ToRule();
        Change(o => o with { Rules = o.Rules.Select(r => r.Kind.Equals(rule.Kind) ? rule : r).ToList() });
    }

    private void LoadRules()
    {
        Rules.Clear();
        foreach (var rule in Model.Options.Rules)
        {
            Rules.Add(new WindowRuleRow(rule, _choices, _titleBarChoices, OnRuleChanged));
        }

        HasRules = Rules.Count > 0;
    }

    private void LoadAddable()
    {
        var options = Model.Options;
        _addable = _context.Controller.OpenWindowKinds().Where(k => options.RuleFor(k) is null).ToList();
        AddableNames.Clear();
        foreach (var kind in _addable)
        {
            AddableNames.Add($"{kind.Process} · {kind.WindowClass}");
        }

        AddIndex = -1;
        CanAdd = false;
    }
}
