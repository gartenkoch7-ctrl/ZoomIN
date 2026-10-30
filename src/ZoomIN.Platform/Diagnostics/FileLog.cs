using System.Globalization;
using System.Text;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Appends log lines to a daily file under %LocalAppData%\ZoomIN\logs\.
/// Thread-safe; failures while writing are swallowed so logging can never crash the app.
/// </summary>
public sealed class FileLog : ILog
{
    private readonly object _gate = new();
    private readonly string _directory;

    public FileLog()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZoomIN",
            "logs"))
    {
    }

    public FileLog(string directory)
    {
        _directory = directory;
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var now = DateTime.Now;
        var line = new StringBuilder()
            .Append(now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(" [").Append(level).Append("] ")
            .Append(message);
        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        var file = Path.Combine(_directory, $"zoomin-{now:yyyyMMdd}.log");
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                File.AppendAllText(file, line.AppendLine().ToString());
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
