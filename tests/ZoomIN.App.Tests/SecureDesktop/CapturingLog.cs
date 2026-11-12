using Microsoft.Extensions.Logging;

namespace ZoomIN.App.Tests.SecureDesktop;

/// <summary>Keeps every log line with its event id and level.</summary>
internal sealed class CapturingLog<T> : ILogger<T>
{
    public List<(int Id, LogLevel Level, string Text)> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (Lines)
        {
            Lines.Add((eventId.Id, logLevel, formatter(state, exception)));
        }
    }

    public string Text(int id) => Assert.Single(Lines, line => line.Id == id).Text;
}
