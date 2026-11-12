using System.Globalization;
using ZoomIN.Core.Localization;

namespace ZoomIN.App.Localization;

/// <summary>All text tables of the app, merged per language, and the localizer built from them.</summary>
internal static class AppTexts
{
    private static readonly IReadOnlyDictionary<string, string> s_german = Merge(
        TextsGerman.All, TextsGermanCards.All, TextsGermanUpdates.All, TextsGermanDiagnostics.All, TextsGermanWindowFilter.All,
        TextsGermanOverview.All, TextsGermanWindowDrag.All);

    private static readonly IReadOnlyDictionary<string, string> s_english = Merge(
        TextsEnglish.All, TextsEnglishCards.All, TextsEnglishUpdates.All, TextsEnglishDiagnostics.All, TextsEnglishWindowFilter.All,
        TextsEnglishOverview.All, TextsEnglishWindowDrag.All);

    /// <summary>A new localizer in English; "System" follows the Windows display language.</summary>
    public static Localizer CreateLocalizer() =>
        new(s_german, s_english, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de");

    private static Dictionary<string, string> Merge(params IReadOnlyDictionary<string, string>[] parts) =>
        parts.SelectMany(part => part).ToDictionary(p => p.Key, p => p.Value);
}
