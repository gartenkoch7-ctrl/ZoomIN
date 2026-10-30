using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Views;

namespace ZoomIN.App.ViewModels;

/// <summary>Profile list and commands (M7). File dialogs are handled by the page.</summary>
public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly PageContext _context;

    public ProfilesViewModel(PageContext context)
    {
        _context = context;
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

    public void Create() => Report(_context.Profiles.CreateFromCurrent(NewName.Trim()));

    public void Activate()
    {
        if (Selected is { } name)
        {
            _context.Profiles.SwitchTo(name);
            Refresh();
        }
    }

    public void Rename()
    {
        if (Selected is { } name)
        {
            Report(_context.Profiles.Rename(name, NewName.Trim()));
        }
    }

    public void Delete()
    {
        if (Selected is { } name && _context.Profiles.Delete(name))
        {
            Refresh();
        }
    }

    public void Export(string filePath)
    {
        if (Selected is { } name)
        {
            _context.Profiles.Export(name, filePath);
        }
    }

    public void Import(string filePath)
    {
        Message = _context.Profiles.Import(filePath) is null ? Text.Get("ProfilesInvalidName") : string.Empty;
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
        foreach (var name in _context.Profiles.Names())
        {
            Names.Add(name);
        }

        Active = _context.Profiles.Active;
        Selected = Active;
    }
}
