namespace ZoomIN.Core.Settings;

/// <summary>Named profiles (M7). A profile is a full <see cref="AppSettings"/>; only its display part is used.</summary>
public interface IProfileStore
{
    IReadOnlyList<string> Names();

    AppSettings? Load(string name);

    void Save(string name, AppSettings settings);

    void Delete(string name);

    /// <returns>False if the new name is invalid or taken.</returns>
    bool Rename(string oldName, string newName);

    void Export(string name, string filePath);

    /// <returns>Name of the imported profile, or null if the file is not a valid profile.</returns>
    string? Import(string filePath);
}
