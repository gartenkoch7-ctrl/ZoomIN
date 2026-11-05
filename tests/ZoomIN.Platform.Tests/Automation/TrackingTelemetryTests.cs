using Microsoft.Extensions.Logging;
using ZoomIN.Platform.Automation;

namespace ZoomIN.Platform.Tests.Automation;

/// <summary>A caret source is logged once per process, however often the reading alternates.</summary>
public sealed class TrackingTelemetryTests
{
    private static readonly uint s_ownProcess = (uint)Environment.ProcessId;

    [Fact]
    public void BlinkingBetweenSources_LogsEachSourceOnce()
    {
        var log = new CapturingLogger();
        var telemetry = new TrackingTelemetry(log);

        // Notepad while blinking: system caret, UI Automation, system caret, …
        for (var i = 0; i < 20; i++)
        {
            telemetry.Observe(s_ownProcess, i % 2 == 0 ? CaretSource.System : CaretSource.Uia, 1);
        }

        Assert.Equal(2, log.Lines.Count);
        Assert.Contains(log.Lines, line => line.EndsWith(": system", StringComparison.Ordinal));
        Assert.Contains(log.Lines, line => line.EndsWith(": uia", StringComparison.Ordinal));
        Assert.All(log.Lines, line => Assert.StartsWith("Caret source in " + System.Diagnostics.Process.GetCurrentProcess().ProcessName, line, StringComparison.Ordinal));
    }

    [Fact]
    public void NoCaretOrNoProcess_IsNotLogged()
    {
        var log = new CapturingLogger();
        var telemetry = new TrackingTelemetry(log);

        telemetry.Observe(s_ownProcess, CaretSource.None, 1);
        telemetry.Observe(0, CaretSource.System, 1);

        Assert.Empty(log.Lines);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }
}
