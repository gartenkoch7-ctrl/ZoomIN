using System.Globalization;
using System.Text;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Where logs and operations are kept on the PC (ADR-0019): one file per hour and kind under
/// %LocalAppData%\ZoomIN\logs (SYSTEM's profile for the secure desktop). Hourly files let the
/// retention cut at "N × 24 hours" instead of whole calendar days. At most
/// <see cref="DailyLimitBytes"/> per calendar day; beyond that only errors are written.
/// Several ZoomIN processes append to the same files, so every write opens with shared access.
/// </summary>
internal sealed class TelemetryFiles
{
    public const long DailyLimitBytes = 20L * 1024 * 1024;

    private const string Prefix = "zoomin-";
    private const string HourFormat = "yyyyMMdd-HH";

    // Re-reading the file sizes for every batch would cost a directory scan per second.
    private static readonly TimeSpan UsageRefresh = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly TimeProvider _time;
    private int _retentionDays = 1;
    private DateTimeOffset _usageReadAt = DateTimeOffset.MinValue;
    private long _usedToday;
    private DateOnly _usageDay;

    public TelemetryFiles(string directory, TimeProvider time)
    {
        Folder = directory;
        _time = time;
    }

    public string Folder { get; }

    public int RetentionDays
    {
        get => Volatile.Read(ref _retentionDays);
        set => Volatile.Write(ref _retentionDays, Math.Clamp(value, 1, 7));
    }

    public string LogFile(DateTimeOffset at) => Path.Combine(Folder, Prefix + Hour(at) + ".log");

    public string TraceFile(DateTimeOffset at) => Path.Combine(Folder, Prefix + Hour(at) + ".trace.jsonl");

    /// <summary>
    /// Appends <paramref name="text"/>; returns false if it was dropped (daily limit, file
    /// locked). <paramref name="essential"/> text (errors) ignores the limit.
    /// </summary>
    public bool Append(string path, string text, bool essential)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        lock (_gate)
        {
            if (!essential && UsedToday() + bytes.Length > DailyLimitBytes)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(Folder);
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.Write(bytes);
                _usedToday += bytes.Length;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    /// <summary>Deletes every hour file whose newest entry is older than the retention.</summary>
    public void CleanUp()
    {
        var oldestKept = _time.GetLocalNow().AddDays(-RetentionDays);
        try
        {
            foreach (var file in Directory.EnumerateFiles(Folder, Prefix + "*"))
            {
                if (HourOf(file) is { } start && start.AddHours(1) <= oldestKept)
                {
                    TryDelete(file);
                }
                else if (IsLegacyDailyLog(file, oldestKept))
                {
                    TryDelete(file);
                }
            }
        }
        catch (IOException)
        {
            // Folder missing or unreadable: nothing to clean.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>All files currently kept, oldest first (diagnostics package).</summary>
    public IReadOnlyList<string> KeptFiles()
    {
        try
        {
            return [.. Directory.EnumerateFiles(Folder, Prefix + "*").Order(StringComparer.Ordinal)];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private long UsedToday()
    {
        var now = _time.GetLocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        if (today != _usageDay || now - _usageReadAt > UsageRefresh)
        {
            _usageDay = today;
            _usageReadAt = now;
            _usedToday = SizeOfDay(today);
        }

        return _usedToday;
    }

    private long SizeOfDay(DateOnly day)
    {
        try
        {
            var pattern = Prefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-*";
            return Directory.EnumerateFiles(Folder, pattern).Sum(file => new FileInfo(file).Length);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static string Hour(DateTimeOffset at) => at.ToString(HourFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset? HourOf(string file)
    {
        var name = Path.GetFileName(file);
        return name.Length >= Prefix.Length + HourFormat.Length
            && DateTime.TryParseExact(name.AsSpan(Prefix.Length, HourFormat.Length), HourFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var hour)
            ? new DateTimeOffset(hour)
            : null;
    }

    // Logs written before M11 were one file per day (zoomin-yyyyMMdd.log).
    private static bool IsLegacyDailyLog(string file, DateTimeOffset oldestKept)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return name.Length == Prefix.Length + 8
            && DateTime.TryParseExact(name.AsSpan(Prefix.Length), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var day)
            && new DateTimeOffset(day.AddDays(1)) <= oldestKept;
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
            // Still open in another ZoomIN process: removed at the next clean-up.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
