using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;
using ZoomIN.Platform.Settings;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// Settings of the secure-desktop instance (ADR-0016): the session user's settings.json if there
/// is a signed-in user, otherwise the copy kept from the last time, otherwise defaults. Everything
/// read is bounded by <see cref="SecureSettingsSanitizer"/>. Never writes to the user's folder;
/// changes made on the secure desktop are not saved.
/// </summary>
public sealed class SecureSettingsStore : ISettingsStore
{
    private const string FileName = "settings.json";

    // Tag of zoomin.secure.settings: where the settings came from ("user", "copy", "default").
    private const string SourceTag = "zoomin.secure.settings_source";

    private readonly ILogger _log;
    private readonly SessionUser _user;
    private readonly string _copyFolder;

    public SecureSettingsStore(ILogger log, SessionUser user, string machineFolder)
    {
        _log = log;
        _user = user;
        _copyFolder = Path.Combine(machineFolder, "secure");
    }

    public AppSettings Load()
    {
        using var operation = Telemetry.Source.StartActivity("zoomin.secure.settings");

        // Read as the user, write the copy as SYSTEM: the copy lives in SYSTEM's own profile.
        if (_user.ZoomFolder is { } folder)
        {
            if (_user.Read(() => Parse(Path.Combine(folder, FileName))) is { } user)
            {
                _log.SettingsSource("the session user's");
                operation?.SetTag(SourceTag, "user");
                KeepCopy(user);
                return user;
            }

            _log.UserSettingsUnusable();
        }

        if (Parse(Path.Combine(_copyFolder, FileName)) is { } copy)
        {
            _log.SettingsSource("the kept copy of the");
            operation?.SetTag(SourceTag, "copy");
            return copy;
        }

        _log.SettingsSource("default");
        operation?.SetTag(SourceTag, "default");
        return new AppSettings();
    }

    /// <summary>Intentionally empty: the secure desktop never writes settings back.</summary>
    public void Save(AppSettings settings)
    {
    }

    /// <returns>Sanitized settings, or null if the file is missing, refused or not valid JSON.</returns>
    internal static AppSettings? Parse(string path)
    {
        if (GuardedFileReader.TryRead(path) is not { } content)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(content, SettingsJsonContext.Default.AppSettings) is { } settings
                ? SecureSettingsSanitizer.Sanitize(settings)
                : null;
        }
        catch (JsonException)
        {
            // The message could quote file content; the secure log never contains user data.
            return null;
        }
    }

    private void KeepCopy(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_copyFolder);
            var target = Path.Combine(_copyFolder, FileName);
            var temp = target + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
            File.Move(temp, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.CopyNotWritten(ex.GetType().Name);
        }
    }
}
