using System.Globalization;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Settings;

namespace ZoomIN.Setup.Localization;

/// <summary>
/// Static facade over the setup's <see cref="ILocalizer"/> for <c>x:Bind</c> in XAML (ADR-0025);
/// starts in the Windows display language (M9).
/// </summary>
public static class SetupText
{
    private static readonly Localizer s_localizer = CreateLocalizer();

    public static bool IsGerman => s_localizer.IsGerman;

    public static void SetGerman(bool german) => s_localizer.SetLanguage(german ? AppLanguage.German : AppLanguage.English);

    public static string Get(string key) => s_localizer.Get(key);

    public static string Format(string key, params object[] args) => s_localizer.Format(key, args);

    private static Localizer CreateLocalizer()
    {
        var localizer = new Localizer(
            SetupTextsGerman.All, SetupTextsEnglish.All, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de");
        localizer.SetLanguage(AppLanguage.System);
        return localizer;
    }
}
