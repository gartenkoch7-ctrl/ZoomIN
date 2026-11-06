using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.Core.Filters;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.App.ViewModels;

/// <summary>One own setting on the filter per window page: app + window class, its choice and its title bar.</summary>
public sealed partial class WindowRuleRow : ObservableObject
{
    private readonly Action<WindowRuleRow> _changed;
    private readonly bool _loading;

    public WindowRuleRow(WindowFilterRule rule, IReadOnlyList<string> choices, IReadOnlyList<string> titleBarChoices, Action<WindowRuleRow> changed)
    {
        _loading = true;
        Kind = rule.Kind;
        Choices = choices;
        TitleBarChoices = titleBarChoices;
        _changed = changed;
        ChoiceIndex = IndexOf(rule);
        TitleBarIndex = (int)rule.TitleBar;
        _loading = false;
    }

    public WindowKind Kind { get; }

    public string Name => Kind.Process;

    public string Description => Text.Format("WinFilter.Rule.Desc", Kind.WindowClass);

    public string RemoveName => Text.Format("WinFilter.RemoveFor", Kind.Process);

    public string TitleBarName => Text.Format("WinFilter.TitleBarFor", Kind.Process);

    /// <summary>Automatic, None, then every filter in enum order.</summary>
    public IReadOnlyList<string> Choices { get; }

    /// <summary>One entry per <see cref="WindowTitleBarChoice"/>, in enum order.</summary>
    public IReadOnlyList<string> TitleBarChoices { get; }

    [ObservableProperty]
    public partial int ChoiceIndex { get; set; }

    [ObservableProperty]
    public partial int TitleBarIndex { get; set; }

    public WindowFilterRule ToRule()
    {
        var rule = ChoiceIndex switch
        {
            <= 0 => WindowFilterRule.For(Kind, WindowFilterChoice.Automatic),
            1 => WindowFilterRule.For(Kind, WindowFilterChoice.None),
            _ => WindowFilterRule.For(Kind, WindowFilterChoice.Filter, ColorFilters.DisplayOrder[ChoiceIndex - 2]),
        };
        return rule with { TitleBar = TitleBarIndex > 0 ? (WindowTitleBarChoice)TitleBarIndex : WindowTitleBarChoice.Automatic };
    }

    /// <summary>The choice list as the rows show it.</summary>
    public static IReadOnlyList<string> AllChoices() =>
    [
        Text.Get("WinFilter.Choice.Automatic"),
        Text.Get("WinFilter.Choice.None"),
        .. ColorFilters.DisplayOrder.Select(f => Text.Get("Filter." + f)),
    ];

    /// <summary>The title bar choices as the rows show them.</summary>
    public static IReadOnlyList<string> AllTitleBarChoices() =>
        [.. Enum.GetValues<WindowTitleBarChoice>().Select(c => Text.Get("WinFilter.TitleBar." + c))];

    /// <summary>With "None" the title bar keeps its original colours too, so its choice has no effect.</summary>
    public bool TitleBarEnabled => ChoiceIndex != 1;

    partial void OnChoiceIndexChanged(int value)
    {
        OnPropertyChanged(nameof(TitleBarEnabled));
        if (!_loading && value >= 0)
        {
            _changed(this);
        }
    }

    partial void OnTitleBarIndexChanged(int value)
    {
        if (!_loading && value >= 0)
        {
            _changed(this);
        }
    }

    private static int IndexOf(WindowFilterRule rule) => rule.Choice switch
    {
        WindowFilterChoice.None => 1,
        WindowFilterChoice.Filter => 2 + ColorFilters.DisplayIndexOf(rule.Filter),
        _ => 0,
    };
}
