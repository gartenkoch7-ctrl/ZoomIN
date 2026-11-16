using Microsoft.Extensions.Logging;
using ZoomIN.Core.Filters;
using ZoomIN.Rendering.Schemes;

namespace ZoomIN.Rendering.Tests.Schemes;

/// <summary>Logs 3220/3221 (M18): only when the places or the replacement step change, and without names.</summary>
public sealed class SchemePaletteWatchTests
{
    [Fact]
    public void LogsPaletteAndReplaceStep_OnlyOnChange_WithoutNames()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        var log = new CapturingLogger();
        using var watch = new SchemePaletteWatch(model, log);
        var scheme = new CustomScheme { Id = FilterId.Custom2, Name = "Geheim", Kind = SchemeKind.Tint };

        model.UpdateSchemes(_ => [scheme]);
        model.SetSmoothing(Smoothing.Soft);
        model.UpdateSchemes(_ => [scheme with { Replacements = [new ColorReplacement()] }]);
        model.UpdateSchemes(_ => []);

        Assert.Equal([3220, 3221, 3220, 3221], log.Events);
        Assert.Contains("Custom2 Tint", log.Lines[0], StringComparison.Ordinal);
        Assert.Contains("on: 1", log.Lines[1], StringComparison.Ordinal);
        Assert.Contains("off: 0", log.Lines[3], StringComparison.Ordinal);
        Assert.DoesNotContain(log.Lines, line => line.Contains("Geheim", StringComparison.Ordinal));
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Lines { get; } = [];

        public List<int> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Events.Add(eventId.Id);
            Lines.Add(formatter(state, exception));
        }
    }
}
