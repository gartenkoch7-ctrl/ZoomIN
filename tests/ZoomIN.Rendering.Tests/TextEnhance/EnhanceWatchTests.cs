using Microsoft.Extensions.Logging;
using ZoomIN.Core.Filters;
using ZoomIN.Rendering.TextEnhance;

namespace ZoomIN.Rendering.Tests.TextEnhance;

/// <summary>Logs 3200/3201 (M18, D1): state changes once, value changes only at debug level.</summary>
public sealed class EnhanceWatchTests
{
    [Fact]
    public void LogsOnPausedOff_AndValueChangesOnlyWhileOn()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        var log = new CapturingLogger();
        using var watch = new EnhanceWatch(model, log);

        model.SetEnhancement(new TextEnhancement(0.6, 1));
        model.SetEnhancement(new TextEnhancement(0.7, 1));
        model.SetFilter(new FilterSettings(true, FilterId.Inverted, 1, 0));
        model.SetSmoothing(Smoothing.Soft);
        model.SetEnhancement(TextEnhancement.Off);
        model.SetEnhancement(TextEnhancement.Off);

        Assert.Equal([3200, 3201, 3200, 3200], log.Events);
        Assert.Equal("Text enhancement on: sharpness 60 %, bold 1 px, smoothing Text", log.Lines[0]);
        Assert.Equal(LogLevel.Debug, log.Levels[1]);
        Assert.StartsWith("Text enhancement paused", log.Lines[2], StringComparison.Ordinal);
        Assert.StartsWith("Text enhancement off", log.Lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void StartsSilent_WhenOff_AndLogsTheStartState_WhenOn()
    {
        var log = new CapturingLogger();
        using (new EnhanceWatch(new EffectsModel(DisplayEffects.Default), log))
        {
            Assert.Empty(log.Events);
        }

        var on = DisplayEffects.Default with { Enhancement = new TextEnhancement(0, 2) };
        using var watch = new EnhanceWatch(new EffectsModel(on), log);

        Assert.Equal([3200], log.Events);
        Assert.Equal("on", EnhanceWatch.State(on));
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Lines { get; } = [];

        public List<int> Events { get; } = [];

        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Events.Add(eventId.Id);
            Levels.Add(logLevel);
            Lines.Add(formatter(state, exception));
        }
    }
}
