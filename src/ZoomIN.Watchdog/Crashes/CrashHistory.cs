using System.Globalization;

namespace ZoomIN.Watchdog.Crashes;

/// <summary>
/// Crash timestamps in %LocalAppData%\ZoomIN\crashes.log, one ISO-8601 line each. A file is
/// needed because every restart starts a new watchdog process.
/// </summary>
internal sealed class CrashHistory
{
    private const int KeepLines = 20;
    private readonly string _path;

    public CrashHistory(string path)
    {
        _path = path;
    }

    public static CrashHistory Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN", "crashes.log"));

    /// <summary>Records a crash and returns all known crash times, including this one.</summary>
    public IReadOnlyList<DateTimeOffset> Record(DateTimeOffset time)
    {
        var times = Read();
        times.Add(time);

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllLines(_path, times.TakeLast(KeepLines).Select(t => t.ToString("O", CultureInfo.InvariantCulture)));
        return times;
    }

    private List<DateTimeOffset> Read()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        return File.ReadAllLines(_path)
            .Select(line => DateTimeOffset.TryParse(line, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : (DateTimeOffset?)null)
            .OfType<DateTimeOffset>()
            .ToList();
    }
}
