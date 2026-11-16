using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Hands out loggers once at start-up but lets the telemetry host replace what is behind them:
/// the level filter changes with the diagnostics setting, and the OpenTelemetry pipeline (file,
/// optional OTLP export) is rebuilt when the export address changes – no restart needed. Until the
/// first pipeline exists (built in the background, start-up budget), entries are kept and written
/// once it is there.
/// </summary>
internal sealed class SwitchableLoggerProvider : ILoggerProvider
{
    // A start-up that logs more than this before the pipeline exists is broken anyway.
    private const int MaxEarlyEntries = 500;

    private readonly object _gate = new();
    private readonly List<EarlyEntry> _early = [];
    private Pipeline? _pipeline;

    // Read without a lock on every IsEnabled; a torn read only mixes old and new filter once.
    private TelemetryFilter _filter = TelemetryOptions.Default.Filter;

    public TelemetryFilter Filter
    {
        get => _filter;
        set => _filter = value;
    }

    public ILogger CreateLogger(string categoryName) => new ForwardingLogger(this, categoryName);

    /// <summary>
    /// Uses <paramref name="next"/> from now on and disposes (flushes) the previous pipeline; the
    /// first call also writes what was logged before.
    /// </summary>
    public void Replace(ILoggerFactory next)
    {
        Pipeline? old;
        lock (_gate)
        {
            old = _pipeline;
            _pipeline = new Pipeline(next);
            foreach (var entry in _early)
            {
                using var context = ReplayContextProcessor.Use(entry.Operation);
                _pipeline.Logger(entry.Category).Log(entry.Level, entry.EventId, entry.Message, entry.Exception, static (message, _) => message);
            }

            _early.Clear();
        }

        old?.Factory.Dispose();
    }

    public void Dispose() => Volatile.Read(ref _pipeline)?.Factory.Dispose();

    private void Log<TState>(string category, LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Volatile.Read(ref _pipeline) is { } pipeline)
        {
            pipeline.Logger(category).Log(level, eventId, state, exception, formatter);
            return;
        }

        lock (_gate)
        {
            if (_pipeline is not null)
            {
                _pipeline.Logger(category).Log(level, eventId, state, exception, formatter);
            }
            else if (_early.Count < MaxEarlyEntries)
            {
                _early.Add(new EarlyEntry(category, level, eventId, formatter(state, exception), exception, Activity.Current?.Context ?? default));
            }
        }
    }

    // The operation running when the entry was made: its ids are set on replay (ReplayContextProcessor).
    private sealed record EarlyEntry(string Category, LogLevel Level, EventId EventId, string Message, Exception? Exception, ActivityContext Operation);

    private sealed class Pipeline(ILoggerFactory factory)
    {
        private readonly ConcurrentDictionary<string, ILogger> _loggers = new(StringComparer.Ordinal);

        public ILoggerFactory Factory { get; } = factory;

        public ILogger Logger(string category) => _loggers.GetOrAdd(category, Factory.CreateLogger);
    }

    private sealed class ForwardingLogger(SwitchableLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => Volatile.Read(ref owner._pipeline)?.Logger(category).BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => owner._filter.Allows(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                owner.Log(category, logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
