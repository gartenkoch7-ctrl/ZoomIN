using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Localization;

namespace ZoomIN.App.ViewModels;

/// <summary>One island on the "Zoom" page (M19 B1): which monitor, how large, how far magnified, live or still.</summary>
public sealed partial class IslandRow : ObservableObject
{
    private readonly Action<IslandRow> _stillChanged;
    private readonly bool _loading;

    /// <param name="number">Place in the list, from 1.</param>
    /// <param name="monitor">Monitor number as the user sees it (1, 2, …), or 0 if it is not attached.</param>
    public IslandRow(Island island, int number, int monitor, ILocalizer texts, Action<IslandRow> stillChanged)
    {
        ArgumentNullException.ThrowIfNull(island);
        ArgumentNullException.ThrowIfNull(texts);
        _loading = true;
        Id = island.Id;
        _stillChanged = stillChanged;
        Name = texts.Format("Islands.Row", number);
        var level = island.Level.ToString("0.#", CultureInfo.CurrentCulture);
        var width = Math.Round(island.Source.Width).ToString(CultureInfo.CurrentCulture);
        var height = Math.Round(island.Source.Height).ToString(CultureInfo.CurrentCulture);
        Description = monitor > 0
            ? texts.Format("Islands.Row.Desc", monitor, width, height, level)
            : texts.Format("Islands.Row.Missing", width, height, level);
        RemoveName = texts.Format("Islands.RemoveFor", number);
        StillName = texts.Format("Islands.StillFor", number);
        Still = island.Still;
        _loading = false;
    }

    public int Id { get; }

    public string Name { get; }

    public string Description { get; }

    public string RemoveName { get; }

    public string StillName { get; }

    /// <summary>Still image instead of the live picture.</summary>
    [ObservableProperty]
    public partial bool Still { get; set; }

    partial void OnStillChanged(bool value)
    {
        if (!_loading)
        {
            _stillChanged(this);
        }
    }
}
