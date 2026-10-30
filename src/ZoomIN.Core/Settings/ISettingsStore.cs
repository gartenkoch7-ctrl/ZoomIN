namespace ZoomIN.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/>. Never throws: falls back to defaults.</summary>
public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
