using System.Globalization;

namespace ZoomIN.Setup.Localization;

/// <summary>Setup texts in German and English; starts in the Windows display language (M9).</summary>
public static class SetupText
{
    private static IReadOnlyDictionary<string, string> s_current =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? SetupTextsGerman.All : SetupTextsEnglish.All;

    public static bool IsGerman => ReferenceEquals(s_current, SetupTextsGerman.All);

    public static void SetGerman(bool german) => s_current = german ? SetupTextsGerman.All : SetupTextsEnglish.All;

    public static string Get(string key) => s_current.TryGetValue(key, out var value) ? value : key;

    public static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
