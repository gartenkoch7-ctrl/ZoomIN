using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Tests.Diagnostics;

public sealed class TelemetryOptionsTests
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_IsNormal_OneDay_NoExport()
    {
        var options = TelemetryOptions.Default;

        Assert.Equal(TelemetryLevel.Normal, options.Level);
        Assert.Equal(1, options.RetentionDays);
        Assert.Empty(options.OtlpEndpoint);
        Assert.False(options.IsRaised);
        Assert.Equal(new TelemetryFilter(LogLevel.Warning, Traces: true, Metrics: false), options.Filter);
    }

    [Theory]
    [InlineData(TelemetryLevel.ErrorsOnly, LogLevel.Error, false, false)]
    [InlineData(TelemetryLevel.Normal, LogLevel.Warning, true, false)]
    [InlineData(TelemetryLevel.Verbose, LogLevel.Debug, true, true)]
    public void Levels_MapToFilters(TelemetryLevel level, LogLevel min, bool traces, bool metrics)
    {
        var options = TelemetryOptions.Default with { Level = level };

        Assert.Equal(new TelemetryFilter(min, traces, metrics), options.Filter);
    }

    [Fact]
    public void Custom_LogsOff_StillWritesErrors()
    {
        var options = TelemetryOptions.Default with { Level = TelemetryLevel.Custom, Logs = false, Metrics = true };

        Assert.True(options.Filter.Allows(LogLevel.Error));
        Assert.True(options.Filter.Allows(LogLevel.Critical));
        Assert.False(options.Filter.Allows(LogLevel.Warning));
        Assert.True(options.Filter.Metrics);
    }

    [Fact]
    public void Custom_LogLevelAboveError_IsCappedAtError()
    {
        var options = TelemetryOptions.Default with { Level = TelemetryLevel.Custom, LogLevel = LogLevel.Critical };

        Assert.True(options.Filter.Allows(LogLevel.Error));
    }

    [Theory]
    [InlineData(TelemetryLevel.Verbose, 1)]
    [InlineData(TelemetryLevel.Custom, 1)]
    [InlineData(TelemetryLevel.Normal, 3)]
    public void Raised_FallsBackAfterSevenDays(TelemetryLevel level, int days)
    {
        var chosen = TelemetryOptions.Chosen(TelemetryOptions.Default with { Level = level, RetentionDays = days }, s_now);

        Assert.Equal(s_now + TelemetryOptions.RaiseDuration, chosen.RaisedUntil);
        Assert.Equal(chosen, chosen.Effective(s_now.AddDays(6.9)));

        var later = chosen.Effective(s_now.AddDays(7));
        Assert.Equal(TelemetryLevel.Normal, later.Level);
        Assert.Equal(1, later.RetentionDays);
        Assert.Null(later.RaisedUntil);
    }

    [Fact]
    public void ErrorsOnly_IsNotRaised_AndStaysAfterFallback()
    {
        var chosen = TelemetryOptions.Chosen(TelemetryOptions.Default with { Level = TelemetryLevel.ErrorsOnly, RetentionDays = 5 }, s_now);

        var later = chosen.Effective(s_now.AddDays(8));

        Assert.Equal(TelemetryLevel.ErrorsOnly, later.Level);
        Assert.Equal(1, later.RetentionDays);
    }

    [Fact]
    public void RaisedWithoutEnd_EditedByHand_FallsBackAtOnce()
    {
        var edited = TelemetryOptions.Default with { Level = TelemetryLevel.Verbose, RaisedUntil = null };

        Assert.Equal(TelemetryLevel.Normal, edited.Effective(s_now).Level);
    }

    [Fact]
    public void Normal_ChosenAgain_ClearsTheWindow()
    {
        var chosen = TelemetryOptions.Chosen(TelemetryOptions.Default, s_now);

        Assert.Null(chosen.RaisedUntil);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(9, 7)]
    public void RetentionDays_AreClamped(int days, int expected)
    {
        Assert.Equal(expected, (TelemetryOptions.Default with { RetentionDays = days }).Normalized().RetentionDays);
    }

    [Fact]
    public void UnknownValuesFromFile_FallBackToDefaults()
    {
        var options = (TelemetryOptions.Default with { Level = (TelemetryLevel)42, LogLevel = LogLevel.None, OtlpEndpoint = "  " }).Normalized();

        Assert.Equal(TelemetryLevel.Normal, options.Level);
        Assert.Equal(LogLevel.Warning, options.LogLevel);
        Assert.Empty(options.OtlpEndpoint);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var options = TelemetryOptions.Chosen(
            new TelemetryOptions(TelemetryLevel.Custom, false, true, true, LogLevel.Information, 4, null, "http://localhost:4317"), s_now);

        var back = new AppSettings().WithTelemetry(options).Telemetry();

        Assert.Equal(options, back);
    }
}
