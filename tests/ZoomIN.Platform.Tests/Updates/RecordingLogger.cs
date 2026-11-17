using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>Keeps every formatted message, so an admin-only test can show why it failed.</summary>
internal sealed class RecordingLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Messages.Add($"{eventId.Id} {formatter(state, exception)} {exception}");
}
