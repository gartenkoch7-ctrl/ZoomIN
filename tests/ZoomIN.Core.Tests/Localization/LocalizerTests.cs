using ZoomIN.Core.Localization;
using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Tests.Localization;

/// <summary>The language is the state of a service (ADR-0025): switching, system language, missing keys.</summary>
public sealed class LocalizerTests
{
    private static readonly Dictionary<string, string> s_german = new() { ["Hello"] = "Hallo {0}" };
    private static readonly Dictionary<string, string> s_english = new() { ["Hello"] = "Hello {0}" };

    [Fact]
    public void StartsInEnglish()
    {
        var texts = new Localizer(s_german, s_english, systemIsGerman: true);

        Assert.False(texts.IsGerman);
        Assert.Equal("Hello {0}", texts.Get("Hello"));
    }

    [Theory]
    [InlineData(AppLanguage.German, false, true)]
    [InlineData(AppLanguage.English, true, false)]
    [InlineData(AppLanguage.System, true, true)]
    [InlineData(AppLanguage.System, false, false)]
    public void SetLanguage_ChoosesTheTable(AppLanguage language, bool systemIsGerman, bool german)
    {
        var texts = new Localizer(s_german, s_english, systemIsGerman);

        texts.SetLanguage(language);

        Assert.Equal(german, texts.IsGerman);
        Assert.Equal(german ? "Hallo {0}" : "Hello {0}", texts.Get("Hello"));
    }

    [Fact]
    public void SetLanguage_RaisesLanguageChanged()
    {
        var texts = new Localizer(s_german, s_english, systemIsGerman: false);
        var raised = 0;
        texts.LanguageChanged += () => raised++;

        texts.SetLanguage(AppLanguage.German);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void MissingKey_ShowsTheKey_AndFormatFillsArguments()
    {
        var texts = new Localizer(s_german, s_english, systemIsGerman: false);
        texts.SetLanguage(AppLanguage.German);

        Assert.Equal("Nope", texts.Get("Nope"));
        Assert.Equal("Hallo Welt", texts.Format("Hello", "Welt"));
    }
}
