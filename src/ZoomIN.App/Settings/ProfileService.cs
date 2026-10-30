using ZoomIN.Core.Settings;

namespace ZoomIN.App.Settings;

/// <summary>
/// Profile commands (M7): switch, cycle (Strg+Alt+P), create, rename, delete, export, import.
/// A profile holds the display settings; hotkeys and app settings are shared (ProfileFields).
/// </summary>
public sealed class ProfileService
{
    private readonly SettingsService _settings;
    private readonly IProfileStore _store;

    public ProfileService(SettingsService settings, IProfileStore store)
    {
        _settings = settings;
        _store = store;
    }

    public event Action? Changed;

    public string Active => _settings.Current.ActiveProfile;

    /// <summary>All profiles; the active one is created on first use.</summary>
    public IReadOnlyList<string> Names()
    {
        if (!_store.Names().Contains(Active))
        {
            _settings.SaveNow();
        }

        return _store.Names();
    }

    public void SwitchTo(string name)
    {
        if (name == Active || _store.Load(name) is not { } profile)
        {
            return;
        }

        // Keep the current state in the profile being left.
        _settings.SaveNow();
        _settings.ApplyDisplay(profile);
        _settings.Update(s => s with { ActiveProfile = name });
        Changed?.Invoke();
    }

    public void Next()
    {
        var names = Names();
        if (names.Count > 1)
        {
            SwitchTo(names[(IndexOf(names, Active) + 1) % names.Count]);
        }
    }

    public bool CreateFromCurrent(string name)
    {
        if (!ProfileFields.IsValidName(name) || _store.Names().Contains(name))
        {
            return false;
        }

        _store.Save(name, _settings.Current);
        Changed?.Invoke();
        return true;
    }

    public bool Rename(string oldName, string newName)
    {
        _settings.SaveNow();
        if (!_store.Rename(oldName, newName))
        {
            return false;
        }

        if (oldName == Active)
        {
            _settings.Update(s => s with { ActiveProfile = newName });
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>The active profile cannot be deleted.</summary>
    public bool Delete(string name)
    {
        if (name == Active)
        {
            return false;
        }

        _store.Delete(name);
        Changed?.Invoke();
        return true;
    }

    public void Export(string name, string filePath)
    {
        _settings.SaveNow();
        _store.Export(name, filePath);
    }

    public string? Import(string filePath)
    {
        var name = _store.Import(filePath);
        Changed?.Invoke();
        return name;
    }

    private static int IndexOf(IReadOnlyList<string> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (names[i] == name)
            {
                return i;
            }
        }

        return -1;
    }
}
