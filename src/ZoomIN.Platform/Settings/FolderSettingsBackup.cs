using System.Globalization;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Settings;

/// <summary>
/// Copies settings.json and the profiles folder to %AppData%\ZoomIN\backups\&lt;version&gt;-&lt;time&gt;\
/// before a version change, so an older version cannot destroy newer settings (ADR-0014).
/// </summary>
public sealed class FolderSettingsBackup : ISettingsBackup
{
    private readonly string _root;
    private readonly ILogger _log;

    public FolderSettingsBackup(ILogger<FolderSettingsBackup> log)
        : this(log, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoomIN"))
    {
    }

    internal FolderSettingsBackup(ILogger<FolderSettingsBackup> log, string root)
    {
        _log = log;
        _root = root;
    }

    public string? Backup(AppVersion from)
    {
        ArgumentNullException.ThrowIfNull(from);
        var settings = Path.Combine(_root, "settings.json");
        var profiles = Path.Combine(_root, "profiles");
        if (!File.Exists(settings) && !Directory.Exists(profiles))
        {
            return null;
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = Path.Combine(_root, "backups", $"{from}-{stamp}");
        try
        {
            Directory.CreateDirectory(target);
            if (File.Exists(settings))
            {
                File.Copy(settings, Path.Combine(target, "settings.json"));
            }

            if (Directory.Exists(profiles))
            {
                var profileTarget = Directory.CreateDirectory(Path.Combine(target, "profiles")).FullName;
                foreach (var file in Directory.EnumerateFiles(profiles, "*.json"))
                {
                    File.Copy(file, Path.Combine(profileTarget, Path.GetFileName(file)));
                }
            }

            // Only the folder name: the full path contains the Windows user name (ADR-0019).
            var folder = Path.GetFileName(target);
            _log.BackedUp(folder);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.BackupFailed(ex);
            return null;
        }
    }
}
