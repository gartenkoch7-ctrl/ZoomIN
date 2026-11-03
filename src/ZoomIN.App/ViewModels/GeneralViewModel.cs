using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Views;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.ViewModels;

/// <summary>Language and autostart (updates have their own page, M9). Every change applies and saves at once (M7).</summary>
public sealed partial class GeneralViewModel : ObservableObject
{
    private readonly PageContext _context;
    private readonly bool _ready;

    public GeneralViewModel(PageContext context)
    {
        _context = context;
        var s = context.Settings.Current;
        LanguageIndex = (int)s.Language;
        Autostart = context.Autostart.IsEnabled;
        _ready = true;
    }

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    [ObservableProperty]
    public partial bool Autostart { get; set; }

    partial void OnLanguageIndexChanged(int value)
    {
        if (!_ready || value < 0)
        {
            return;
        }

        var language = (AppLanguage)value;
        _context.Settings.Update(s => s with { Language = language });
        Text.SetLanguage(language);
    }

    partial void OnAutostartChanged(bool value)
    {
        if (_ready)
        {
            _context.Autostart.SetEnabled(value);
        }
    }
}
