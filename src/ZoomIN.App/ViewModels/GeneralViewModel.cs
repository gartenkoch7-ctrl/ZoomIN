using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Views;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.ViewModels;

/// <summary>Language, autostart and update check. Every change applies and saves at once (M7).</summary>
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
        CheckUpdates = s.CheckForUpdates;
        UpdateUrl = s.UpdateFeedUrl;
        _ready = true;
    }

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    [ObservableProperty]
    public partial bool Autostart { get; set; }

    [ObservableProperty]
    public partial bool CheckUpdates { get; set; }

    [ObservableProperty]
    public partial string UpdateUrl { get; set; } = string.Empty;

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

    partial void OnCheckUpdatesChanged(bool value)
    {
        if (_ready)
        {
            _context.Settings.Update(s => s with { CheckForUpdates = value });
        }
    }

    partial void OnUpdateUrlChanged(string value)
    {
        if (_ready)
        {
            _context.Settings.Update(s => s with { UpdateFeedUrl = value.Trim() });
        }
    }
}
