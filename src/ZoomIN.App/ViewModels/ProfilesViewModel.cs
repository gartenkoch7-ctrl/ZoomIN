using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Settings;

namespace ZoomIN.App.ViewModels;

/// <summary>Profile list and commands (M7). File dialogs are handled by the page.</summary>
public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly ProfileService _profiles;

    public ProfilesViewModel(ProfileService profiles)
    {
        _profiles = profiles;
        Refresh();
    }

    public ObservableCollection<string> Names { get; } = [];

    [ObservableProperty]
    public partial string? Selected { get; set; }

    [ObservableProperty]
    public partial string NewName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Active { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public void Create() => Report(_profiles.CreateFromCurrent(NewName.Trim()));

    public void Activate()
    {
        if (Selected is { } name)
        {
            _profiles.SwitchTo(name);
            Refresh();
        }
    }

    public void Rename()
    {
        if (Selected is { } name)
        {
            Report(_profiles.Rename(name, NewName.Trim()));
        }
    }

    public void Delete()
    {
        if (Selected is { } name && _profiles.Delete(name))
        {
            Refresh();
        }
    }

    public void Export(string filePath)
    {
        if (Selected is { } name)
        {
            _profiles.Export(name, filePath);
        }
    }

    public void Import(string filePath)
    {
        Message = _profiles.Import(filePath) is null ? Text.Get("ProfilesInvalidName") : string.Empty;
        Refresh();
    }

    private void Report(bool ok)
    {
        Message = ok ? string.Empty : Text.Get("ProfilesInvalidName");
        if (ok)
        {
            NewName = string.Empty;
            Refresh();
        }
    }

    private void Refresh()
    {
        Names.Clear();
        foreach (var name in _profiles.Names())
        {
            Names.Add(name);
        }

        Active = _profiles.Active;
        Selected = Active;
    }
}
