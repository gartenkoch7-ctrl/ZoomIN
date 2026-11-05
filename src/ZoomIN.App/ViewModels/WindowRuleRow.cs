using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.Core.Filters;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.App.ViewModels;

/// <summary>One own setting on the filter per window page: app + window class and its choice.</summary>
public sealed partial class WindowRuleRow : ObservableObject
{
    private readonly Action<WindowRuleRow> _changed;
    private readonly bool _loading;

    public WindowRuleRow(WindowFilterRule rule, IReadOnlyList<string> choices, Action<WindowRuleRow> changed)
    {
        _loading = true;
        Kind = rule.Kind;
        Choices = choices;
        _changed = changed;
        ChoiceIndex = IndexOf(rule);
        _loading = false;
    }

    public WindowKind Kind { get; }

    public string Name => Kind.Process;

    public string Description => Text.Format("WinFilter.Rule.Desc", Kind.WindowClass);

    public string RemoveName => Text.Format("WinFilter.RemoveFor", Kind.Process);

    /// <summary>Automatic, None, then every filter in enum order.</summary>
    public IReadOnlyList<string> Choices { get; }

    [ObservableProperty]
    public partial int ChoiceIndex { get; set; }

    public WindowFilterRule ToRule() => ChoiceIndex switch
    {
        <= 0 => WindowFilterRule.For(Kind, WindowFilterChoice.Automatic),
        1 => WindowFilterRule.For(Kind, WindowFilterChoice.None),
        _ => WindowFilterRule.For(Kind, WindowFilterChoice.Filter, (ColorFilter)(ChoiceIndex - 2)),
    };

    /// <summary>The choice list as the rows show it.</summary>
    public static IReadOnlyList<string> AllChoices() =>
    [
        Text.Get("WinFilter.Choice.Automatic"),
        Text.Get("WinFilter.Choice.None"),
        .. Enum.GetValues<ColorFilter>().Select(f => Text.Get("Filter." + f)),
    ];

    partial void OnChoiceIndexChanged(int value)
    {
        if (!_loading && value >= 0)
        {
            _changed(this);
        }
    }

    private static int IndexOf(WindowFilterRule rule) => rule.Choice switch
    {
        WindowFilterChoice.None => 1,
        WindowFilterChoice.Filter => 2 + (int)rule.Filter,
        _ => 0,
    };
}
