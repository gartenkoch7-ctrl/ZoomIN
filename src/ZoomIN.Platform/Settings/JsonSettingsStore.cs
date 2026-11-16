using ZoomIN.Core.Folders;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    // Operation tag of zoomin.settings.load: "ok", "missing" (first start) or "unreadable".
    private const string ResultTag = "zoomin.settings.result";
    private const string Unreadable = "unreadable";

    private readonly ILogger _log;
    private readonly string _path;
    private readonly Lock _gate = new();

    public JsonSettingsStore(ILogger<JsonSettingsStore> log, IAppFolders folders)
        : this(log, folders.Settings())
    {
    }

    public JsonSettingsStore(ILogger<JsonSettingsStore> log, string path)
    {
        _log = log;
        _path = path;
    }

    public AppSettings Load()
    {
        using var operation = Telemetry.Source.StartActivity("zoomin.settings.load");
        var (settings, result) = LoadFile();
        operation?.SetTag(ResultTag, result);
        if (result == Unreadable)
        {
            operation?.SetStatus(ActivityStatusCode.Error, result);
        }

        return settings;
    }

    private (AppSettings Settings, string Result) LoadFile()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return (new AppSettings(), "missing");
            }

            try
            {
                var json = File.ReadAllText(_path);
                var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();

                // A hand-edited file may contain "null" for the levels.
                return (settings with
                {
                    MonitorZoomLevels = settings.MonitorZoomLevels ?? new Dictionary<string, double>(),
                    LensMonitorZoomLevels = settings.LensMonitorZoomLevels ?? new Dictionary<string, double>(),
                    DockedMonitorZoomLevels = settings.DockedMonitorZoomLevels ?? new Dictionary<string, double>(),
                    MonitorFilters = settings.MonitorFilters ?? new Dictionary<string, MonitorFilter>(),
                }, "ok");
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                _log.SettingsUnreadable(ex, Path.GetFileName(_path));
                BackUpBrokenFile();
                return (new AppSettings(), Unreadable);
            }
        }
    }

    public void Save(AppSettings settings)
    {
        using var operation = Telemetry.Source.StartActivity("zoomin.settings.save");
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
                _log.SaveFailed(ex, Path.GetFileName(_path));
                operation?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
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
            _log.BrokenBackupFailed(ex.Message);
        }
    }
}
