using ZoomIN.Core.Settings;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// The session user's profiles, read-only, so Strg+Alt+P also works on the secure desktop
/// (ADR-0016). Same checks as <see cref="SecureSettingsStore"/>, read as the user; without a
/// signed-in user there are no profiles. Every changing call does nothing.
/// </summary>
public sealed class SecureProfileStore : IProfileStore
{
    private const int MaxProfiles = 32;

    private readonly SessionUser _user;
    private readonly string? _directory;

    public SecureProfileStore(SessionUser user)
    {
        _user = user;
        _directory = user.ZoomFolder is null ? null : Path.Combine(user.ZoomFolder, "profiles");
    }

    public IReadOnlyList<string> Names() => _directory is null ? [] : _user.Read(() => List(_directory));

    public AppSettings? Load(string name) =>
        _directory is not null && ProfileFields.IsValidName(name)
            ? _user.Read(() => SecureSettingsStore.Parse(Path.Combine(_directory, name + ".json")))
            : null;

    public void Save(string name, AppSettings settings)
    {
    }

    public void Delete(string name)
    {
    }

    public bool Rename(string oldName, string newName) => false;

    public void Export(string name, string filePath)
    {
    }

    public string? Import(string filePath) => null;

    private static List<string> List(string directory)
    {
        try
        {
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OfType<string>()
                    .Where(ProfileFields.IsValidName)
                    .Take(MaxProfiles)
                    .Order(StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
