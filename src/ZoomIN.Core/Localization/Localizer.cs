using System.Globalization;
using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Localization;

/// <summary>
/// Texts from two dictionaries (M7: own dictionaries instead of .resw/MRT, because switching the
/// language of an unpackaged WinUI app at runtime is unreliable with MRT). Starts in English.
/// </summary>
public sealed class Localizer : ILocalizer
{
    private readonly IReadOnlyDictionary<string, string> _german;
    private readonly IReadOnlyDictionary<string, string> _english;
    private readonly bool _systemIsGerman;
    private IReadOnlyDictionary<string, string> _current;

    /// <param name="systemIsGerman">The Windows display language is German (read by the composition root).</param>
    public Localizer(IReadOnlyDictionary<string, string> german, IReadOnlyDictionary<string, string> english, bool systemIsGerman)
    {
        _german = german;
        _english = english;
        _systemIsGerman = systemIsGerman;
        _current = english;
    }

    public event Action? LanguageChanged;

    public bool IsGerman { get; private set; }

    public void SetLanguage(AppLanguage language)
    {
        IsGerman = language == AppLanguage.German || (language == AppLanguage.System && _systemIsGerman);
        Volatile.Write(ref _current, IsGerman ? _german : _english);
        LanguageChanged?.Invoke();
    }

    public string Get(string key) => Volatile.Read(ref _current).TryGetValue(key, out var value) ? value : key;

    public string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
