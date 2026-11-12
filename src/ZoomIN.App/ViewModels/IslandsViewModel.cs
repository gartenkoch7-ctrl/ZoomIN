using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Islands;
using ZoomIN.App.Settings;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Monitors;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// The card "Inseln" on the "Zoom" page (M19 B1): shown or hidden, the list of islands with live or
/// still and remove, and "Bearbeiten starten". The edit mode and shortcuts change the islands too,
/// so the page calls <see cref="Refresh"/> regularly; rows are rebuilt only when the islands changed.
/// </summary>
public sealed partial class IslandsViewModel : ObservableObject
{
    private readonly IslandModel _model;
    private readonly IslandController _controller;
    private readonly IMonitorProvider _monitors;
    private readonly ILocalizer _texts;
    private IslandSet? _shown;
    private bool _loading;

    public IslandsViewModel(SettingsService settings, IslandController controller, IMonitorProvider monitors, ILocalizer texts)
    {
        _model = settings.Models.Islands;
        _controller = controller;
        _monitors = monitors;
        _texts = texts;
        Refresh();
    }

    public ObservableCollection<IslandRow> Rows { get; } = [];

    public bool NoIslands => Rows.Count == 0;

    /// <summary>Ctrl+Alt+I as a switch.</summary>
    [ObservableProperty]
    public partial bool Visible { get; set; }

    /// <summary>Reads the islands again; rebuilds the rows only if they changed.</summary>
    public void Refresh()
    {
        var scene = _model.Scene;
        _loading = true;
        Visible = scene.Visible;
        _loading = false;
        if (ReferenceEquals(scene.Set, _shown))
        {
            return;
        }

        _shown = scene.Set;
        Rows.Clear();
        var monitors = _monitors.MonitorIds();
        for (var i = 0; i < scene.Set.Count; i++)
        {
            var island = scene.Set[i];
            var monitor = IndexOf(monitors, island.MonitorId) + 1;
            Rows.Add(new IslandRow(island, i + 1, monitor, _texts, OnStillChanged));
        }

        OnPropertyChanged(nameof(NoIslands));
    }

    /// <summary>Starts the edit mode (or ends it if it runs).</summary>
    public void Edit() => _controller.ToggleEdit();

    public void Remove(IslandRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _model.Remove(row.Id);
        Refresh();
    }

    partial void OnVisibleChanged(bool value)
    {
        if (!_loading && value != _model.Scene.Visible)
        {
            _controller.Toggle();
        }
    }

    // The row already shows the new state: keep it (and the keyboard focus on its switch), unless the
    // islands changed meanwhile or the island is gone – then the next Refresh rebuilds the rows.
    private void OnStillChanged(IslandRow row)
    {
        var before = _model.Scene.Set;
        if (_model.SetStill(row.Id, row.Still) && ReferenceEquals(before, _shown))
        {
            _shown = _model.Scene.Set;
        }
    }

    private static int IndexOf(IReadOnlyList<string> monitors, string id)
    {
        for (var i = 0; i < monitors.Count; i++)
        {
            if (string.Equals(monitors[i], id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
