using ZoomIN.Core.Filters;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

public sealed class WindowFilterOptionsTests
{
    private static readonly WindowKind s_word = new("WINWORD.EXE", "OpusApp");

    [Fact]
    public void Defaults_AreOn_Inverted_Medium()
    {
        var options = WindowFilterOptions.Default;

        Assert.True(options.Enabled);
        Assert.Equal(ColorFilter.Inverted, options.AutoFilter);
        Assert.Equal(60, options.ThresholdPercent);
        Assert.Empty(options.Rules);
    }

    [Theory]
    [InlineData(WindowFilterSensitivity.Low, 75)]
    [InlineData(WindowFilterSensitivity.Medium, 60)]
    [InlineData(WindowFilterSensitivity.High, 45)]
    public void Sensitivity_SetsThreshold(WindowFilterSensitivity sensitivity, int percent)
    {
        Assert.Equal(percent, (WindowFilterOptions.Default with { Sensitivity = sensitivity }).ThresholdPercent);
    }

    [Theory]
    [InlineData(5, WindowFilterOptions.MinPercent)]
    [InlineData(70, 70)]
    [InlineData(100, WindowFilterOptions.MaxPercent)]
    public void CustomPercent_IsBounded(int percent, int expected)
    {
        var options = WindowFilterOptions.Default with { Sensitivity = WindowFilterSensitivity.Custom, CustomPercent = percent };

        Assert.Equal(expected, options.ThresholdPercent);
    }

    [Fact]
    public void Kind_IgnoresCase()
    {
        var options = WindowFilterOptions.Default.WithRule(WindowFilterRule.For(s_word, WindowFilterChoice.None));

        Assert.NotNull(options.RuleFor(new WindowKind("winword.exe", "opusapp")));
        Assert.Null(options.RuleFor(new WindowKind("winword.exe", "_WwG")));
    }

    [Fact]
    public void WithRule_ReplacesTheRuleOfTheSameKind_AndPutsItFirst()
    {
        var excel = new WindowKind("excel.exe", "XLMAIN");
        var options = WindowFilterOptions.Default
            .WithRule(WindowFilterRule.For(s_word, WindowFilterChoice.None))
            .WithRule(WindowFilterRule.For(excel, WindowFilterChoice.None))
            .WithRule(WindowFilterRule.For(s_word, WindowFilterChoice.Filter, ColorFilter.Grayscale));

        Assert.Equal(2, options.Rules.Count);
        Assert.Equal(s_word, options.Rules[0].Kind);
        Assert.Equal(ColorFilter.Grayscale, options.Rules[0].Filter);
    }

    [Fact]
    public void WithoutRule_RemovesOnlyThatKind()
    {
        var excel = new WindowKind("excel.exe", "XLMAIN");
        var options = WindowFilterOptions.Default
            .WithRule(WindowFilterRule.For(s_word, WindowFilterChoice.None))
            .WithRule(WindowFilterRule.For(excel, WindowFilterChoice.None))
            .WithoutRule(new WindowKind("WinWord.exe", "OPUSAPP"));

        Assert.Equal(excel, Assert.Single(options.Rules).Kind);
    }

    [Fact]
    public void Normalized_DropsBrokenAndDuplicateRules_AndUnknownValues()
    {
        var options = new WindowFilterOptions
        {
            AutoFilter = (ColorFilter)99,
            Sensitivity = (WindowFilterSensitivity)42,
            CustomPercent = 400,
            Rules =
            [
                WindowFilterRule.For(s_word, WindowFilterChoice.None),
                WindowFilterRule.For(new WindowKind("winword.exe", "opusapp"), WindowFilterChoice.Filter),
                WindowFilterRule.For(new WindowKind(string.Empty, "x"), WindowFilterChoice.None),
                WindowFilterRule.For(new WindowKind("a.exe", "b"), (WindowFilterChoice)7),
                new WindowFilterRule { Process = "c.exe", WindowClass = "d", Filter = (ColorFilter)9 },
                null!,
            ],
        }.Normalized();

        Assert.Equal(ColorFilter.Inverted, options.AutoFilter);
        Assert.Equal(WindowFilterSensitivity.Medium, options.Sensitivity);
        Assert.Equal(WindowFilterOptions.MaxPercent, options.CustomPercent);
        Assert.Equal(WindowFilterChoice.None, Assert.Single(options.Rules).Choice);
    }
}
