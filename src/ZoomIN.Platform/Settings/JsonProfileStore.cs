using ZoomIN.Core.Folders;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Settings;

namespace ZoomIN.Platform.Settings;

/// <summary>Profiles as JSON files in %AppData%\ZoomIN\profiles\ (ADR-0004, M7).</summary>
public sealed class JsonProfileStore : IProfileStore
{
    private readonly ILogger _log;
    private readonly string _directory;

    public JsonProfileStore(ILogger<JsonProfileStore> log, IAppFolders folders)
        : this(log, folders.Profiles())
    {
    }

    public JsonProfileStore(ILogger<JsonProfileStore> log, string directory)
    {
        _log = log;
        _directory = directory;
    }

    public IReadOnlyList<string> Names() =>
        Directory.Exists(_directory)
            ? Directory.GetFiles(_directory, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.CurrentCultureIgnoreCase).ToList()
            : [];

    public AppSettings? Load(string name) => ProfileFields.IsValidName(name) ? Read(PathOf(name)) : null;

    public void Save(string name, AppSettings settings)
    {
        if (!ProfileFields.IsValidName(name))
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        var temp = PathOf(name) + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
        File.Move(temp, PathOf(name), overwrite: true);
    }

    public void Delete(string name)
    {
        if (ProfileFields.IsValidName(name))
        {
            File.Delete(PathOf(name));
        }
    }

    public bool Rename(string oldName, string newName)
    {
        if (!ProfileFields.IsValidName(oldName) || !ProfileFields.IsValidName(newName) || File.Exists(PathOf(newName)))
        {
            return false;
        }

        File.Move(PathOf(oldName), PathOf(newName));
        return true;
    }

    public void Export(string name, string filePath) => File.Copy(PathOf(name), filePath, overwrite: true);

    public string? Import(string filePath)
    {
        if (Read(filePath) is not { } settings)
        {
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(filePath);
        var name = ProfileFields.IsValidName(baseName) ? baseName : "Import";
        for (var i = 2; File.Exists(PathOf(name)); i++)
        {
            name = $"{(ProfileFields.IsValidName(baseName) ? baseName : "Import")} {i}";
        }

        Save(name, settings);
        return name;
    }

    private string PathOf(string name) => Path.Combine(_directory, name + ".json");

    private AppSettings? Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJsonContext.Default.AppSettings) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.ProfileUnreadable(Path.GetFileName(path), ex.Message);
            return null;
        }
    }
}
