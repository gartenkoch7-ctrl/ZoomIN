using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Writes log records as text lines to the hourly log file, one batch per write, on the batch
/// processor's thread (never the caller's). Each line carries role, process id and – inside an
/// operation – trace and span id, so the log can be matched with the operations file.
/// </summary>
internal sealed class LogFileExporter : BaseExporter<LogRecord>
{
    private readonly TelemetryFiles _files;
    private readonly string _process;
    private readonly TimeProvider _time;
    private DateOnly _limitNoticeDay;

    public LogFileExporter(TelemetryFiles files, string role, TimeProvider time)
    {
        _files = files;
        _process = string.Create(CultureInfo.InvariantCulture, $"{role}#{Environment.ProcessId}");
        _time = time;
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        var normal = new StringBuilder();
        var errors = new StringBuilder();
        foreach (var record in batch)
        {
            var line = record.LogLevel >= LogLevel.Error ? errors : normal;
            Format(record, line);
        }

        var path = _files.LogFile(_time.GetLocalNow());
        if (normal.Length > 0 && !_files.Append(path, normal.ToString(), essential: false))
        {
            NoticeLimit(path);
        }

        if (errors.Length > 0)
        {
            _files.Append(path, errors.ToString(), essential: true);
        }

        return ExportResult.Success;
    }

    private void Format(LogRecord record, StringBuilder line)
    {
        var at = new DateTimeOffset(record.Timestamp).ToLocalTime();
        line.Append(at.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(" [").Append(Level(record.LogLevel)).Append("] ")
            .Append(_process).Append(' ')
            .Append(ShortCategory(record.CategoryName));
        if (record.EventId.Id != 0)
        {
            line.Append('[').Append(record.EventId.Id.ToString(CultureInfo.InvariantCulture)).Append(']');
        }

        line.Append(": ").Append(record.FormattedMessage ?? record.Body);
        if (record.TraceId != default)
        {
            line.Append(" trace=").Append(record.TraceId.ToHexString()).Append(" span=").Append(record.SpanId.ToHexString());
        }

        line.AppendLine();
        if (record.Exception is { } exception)
        {
            line.AppendLine(exception.ToString());
        }
    }

    // Written once per day, as an error so it passes the limit it reports.
    private void NoticeLimit(string path)
    {
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        if (_limitNoticeDay == today)
        {
            return;
        }

        _limitNoticeDay = today;
        _files.Append(path, string.Create(CultureInfo.InvariantCulture,
            $"{_time.GetLocalNow():yyyy-MM-dd HH:mm:ss.fff} [ERROR] {_process} Telemetry: daily limit of {TelemetryFiles.DailyLimitBytes / (1024 * 1024)} MB reached; only errors are written until midnight{Environment.NewLine}"), essential: true);
    }

    private static string Level(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        _ => "FATAL",
    };

    private static string ShortCategory(string? category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return "ZoomIN";
        }

        var dot = category.LastIndexOf('.');
        return dot < 0 ? category : category[(dot + 1)..];
    }
}
