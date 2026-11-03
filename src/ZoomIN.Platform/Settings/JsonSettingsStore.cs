using System.Text.Json;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Settings;

namespace ZoomIN.Platform.Settings;

/// <summary>
/// Stores settings as JSON under %AppData%\ZoomIN\settings.json (ADR-0004).
/// Writes atomically (temp file + replace); a broken file is backed up and defaults are used.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly ILog _log;
    private readonly string _path;
    private readonly Lock _gate = new();

    public JsonSettingsStore(ILog log)
        : this(log, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoomIN", "settings.json"))
    {
    }

    public JsonSettingsStore(ILog log, string path)
    {
        _log = log;
        _path = path;
    }

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(_path);
                var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();

                // A hand-edited file may contain "null" for the levels.
                return settings with
                {
                    MonitorZoomLevels = settings.MonitorZoomLevels ?? new Dictionary<string, double>(),
                    LensMonitorZoomLevels = settings.LensMonitorZoomLevels ?? new Dictionary<string, double>(),
                    DockedMonitorZoomLevels = settings.DockedMonitorZoomLevels ?? new Dictionary<string, double>(),
                    MonitorFilters = settings.MonitorFilters ?? new Dictionary<string, MonitorFilter>(),
                };
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                _log.Error($"Settings file '{_path}' is unreadable; using defaults.", ex);
                BackUpBrokenFile();
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));

                // A crash between write and move leaves the old file intact.
                File.Move(temp, _path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Error($"Saving settings to '{_path}' failed.", ex);
            }
        }
    }

    private void BackUpBrokenFile()
    {
        try
        {
            File.Copy(_path, _path + ".broken", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"Could not back up broken settings file: {ex.Message}");
        }
    }
}
