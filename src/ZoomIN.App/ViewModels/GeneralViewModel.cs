using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.SecureDesktop;
using ZoomIN.App.Settings;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Language, autostart and the sign-in screen (updates have their own page, M9). Every change
/// applies and saves at once (M7); the sign-in screen switch asks for administrator rights (ADR-0016).
/// Both switches show the system's state, read anew each time the page opens (a new view model per
/// navigation): the user can delete the autostart task in the Task Scheduler (ADR-0030).
/// </summary>
public sealed partial class GeneralViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IAutostart _autostart;
    private readonly ISecureDesktopSwitch _secureDesktop;
    private readonly bool _ready;
    private bool _reverting;

    public GeneralViewModel(SettingsService settings, IAutostart autostart, ISecureDesktopSwitch secureDesktop)
    {
        _settings = settings;
        _autostart = autostart;
        _secureDesktop = secureDesktop;
        var s = settings.Current;
        LanguageIndex = (int)s.Language;
        Autostart = autostart.IsEnabled;
        SecureDesktopAvailable = secureDesktop.IsAvailable;
        SecureDesktop = secureDesktop.IsEnabled;
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
        _settings.Update(s => s with { Language = language });
        Text.SetLanguage(language);
    }

    partial void OnAutostartChanged(bool value)
    {
        if (!_ready || _reverting)
        {
            return;
        }

        _autostart.SetEnabled(value);
        _ = ShowRealAutostartAsync(value);
    }

    // The scheduled task (ADR-0030, Q9) can fail without an exception: show the real state, but
    // only after the binding has finished writing the switch's value into this property.
    private async Task ShowRealAutostartAsync(bool value)
    {
        await Task.Yield();
        bool actual;
        try
        {
            actual = _autostart.IsEnabled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Unreadable: keep what the user chose; the page reads it anew next time.
            return;
        }

        if (actual != value)
        {
            _reverting = true;
            Autostart = actual;
            _reverting = false;
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
        await Task.Run(() => _secureDesktop.RequestChange(value));

        // "No" at the prompt or a failure: show the real state again.
        var actual = _secureDesktop.IsEnabled;
        if (actual != value)
        {
            _reverting = true;
            SecureDesktop = actual;
            _reverting = false;
        }
    }
}
