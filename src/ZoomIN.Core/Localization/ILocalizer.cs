using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Localization;

/// <summary>
/// UI texts in German and English (ADR-0025): the language is the state of this service, not of a
/// static class. App and setup each have one; XAML reaches it through their static facade.
/// </summary>
public interface ILocalizer
{
    /// <summary>German texts are shown.</summary>
    bool IsGerman { get; }

    /// <summary>Raised after <see cref="SetLanguage"/>, on the calling thread.</summary>
    event Action? LanguageChanged;

    /// <summary>Switches the texts; <see cref="AppLanguage.System"/> follows the Windows display language.</summary>
    void SetLanguage(AppLanguage language);

    /// <summary>The text for <paramref name="key"/>, or the key itself if there is none.</summary>
    string Get(string key);

    /// <summary>The text for <paramref name="key"/> with the arguments filled in (current culture).</summary>
    string Format(string key, params object[] args);
}
