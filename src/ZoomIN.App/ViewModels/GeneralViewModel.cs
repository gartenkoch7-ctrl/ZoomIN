using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.SecureDesktop;
using ZoomIN.App.Views;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Language, autostart and the sign-in screen (updates have their own page, M9). Every change
/// applies and saves at once (M7); the sign-in screen switch asks for administrator rights (ADR-0016).
/// </summary>
public sealed partial class GeneralViewModel : ObservableObject
{
    private readonly PageContext _context;
    private readonly bool _ready;
    private bool _reverting;

    public GeneralViewModel(PageContext context)
    {
        _context = context;
        var s = context.Settings.Current;
        LanguageIndex = (int)s.Language;
        Autostart = context.Autostart.IsEnabled;
        SecureDesktopAvailable = context.SecureDesktopSwitch.IsAvailable;
        SecureDesktop = context.SecureDesktopSwitch.IsEnabled;
        _ready = true;
    }

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    [ObservableProperty]
    public partial bool Autostart { get; set; }

    [ObservableProperty]
    public partial bool SecureDesktop { get; set; }

    /// <summary>False for developer builds and the portable ZIP: Windows only starts the copy in Program Files.</summary>
    public bool SecureDesktopAvailable { get; }

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

    partial void OnSecureDesktopChanged(bool value)
    {
        if (_ready && !_reverting)
        {
            _ = ChangeSecureDesktopAsync(value);
        }
    }

    private async Task ChangeSecureDesktopAsync(bool value)
    {
        // The administrator prompt blocks until answered; keep the UI responsive meanwhile.
        await Task.Run(() => _context.SecureDesktopSwitch.RequestChange(value));

        // "No" at the prompt or a failure: show the real state again.
        var actual = _context.SecureDesktopSwitch.IsEnabled;
        if (actual != value)
        {
            _reverting = true;
            SecureDesktop = actual;
            _reverting = false;
        }
    }
}
