using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Islands;
using ZoomIN.App.Settings;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Monitors;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// The card "Inseln" on the "Zoom" page (M19 B1): shown or hidden, the list of islands with live or
/// still and remove, and "Bearbeiten starten". The edit mode and shortcuts change the islands too,
/// so the page lets it <see cref="Watch"/> them; rows are rebuilt only when the saved islands changed.
/// </summary>
public sealed partial class IslandsViewModel : ObservableObject
{
    private readonly IslandModel _model;
    private readonly IslandController _controller;
    private readonly IslandVisibility _visibility;
    private readonly IMonitorProvider _monitors;
    private readonly ILocalizer _texts;
    private readonly ILogger _log;
    private IslandSet? _shown;
    private bool _loading;
    private Action? _watching;

    public IslandsViewModel(
        SettingsService settings, IslandController controller, IslandVisibility visibility, IMonitorProvider monitors, ILocalizer texts,
        ILogger<IslandsViewModel> log)
    {
        _model = settings.Models.Islands;
        _controller = controller;
        _visibility = visibility;
        _monitors = monitors;
        _texts = texts;
        _log = log;
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

        // The saved islands: a drag of the edit mode would rebuild the rows on every refresh.
        var set = _model.Committed;
        _loading = false;
        if (ReferenceEquals(set, _shown))
        {
            return;
        }

        _shown = set;
        Rows.Clear();
        var monitors = _monitors.MonitorIds().ToList();
        for (var i = 0; i < set.Count; i++)
        {
            var island = set[i];
            var monitor = DisplayNumber(island.MonitorId, monitors);
            Rows.Add(new IslandRow(island, i + 1, monitor, _texts, OnStillChanged));
        }

        OnPropertyChanged(nameof(NoIslands));
    }

    /// <summary>Starts the edit mode (or ends it if it runs).</summary>
    public void Edit() => _controller.ToggleEdit("settings");

    /// <summary>
    /// Refreshes after every saved change (edit mode, shortcut, profile switch), through
    /// <paramref name="onUiThread"/>: the model raises its events on the thread of the change.
    /// </summary>
    public void Watch(Action<Action> onUiThread)
    {
        Unwatch();
        _watching = () => onUiThread(Refresh);
        _model.Saved += _watching;
        Refresh(); // changes while nobody watched
    }

    public void Unwatch()
    {
        if (_watching is not null)
        {
            _model.Saved -= _watching;
            _watching = null;
        }
    }

    public void Remove(IslandRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_model.Remove(row.Id))
        {
            var number = Rows.IndexOf(row) + 1;
            _log.IslandChangedOnPage(number, "removed");
        }

        Refresh();
    }

    partial void OnVisibleChanged(bool value)
    {
        if (!_loading)
        {
            _visibility.Set(value);
        }
    }

    // The row already shows the new state: keep it (and the keyboard focus on its switch), unless the
    // islands changed meanwhile or the island is gone – then the next Refresh rebuilds the rows.
    private void OnStillChanged(IslandRow row)
    {
        var before = _model.Committed;
        var changed = _model.SetStill(row.Id, row.Still);
        if (changed)
        {
            var number = Rows.IndexOf(row) + 1;
            var kind = row.Still ? "still" : "live";
            _log.IslandChangedOnPage(number, kind);
        }

        var after = _model.Committed;
        if (changed && ReferenceEquals(before, _shown) && OnlyStillDiffers(before, after, row.Id))
        {
            _shown = after;
        }
    }

    // Nothing else committed in between (another thread): otherwise the rows must be rebuilt.
    private static bool OnlyStillDiffers(IslandSet before, IslandSet after, int id)
    {
        if (before.Count != after.Count)
        {
            return false;
        }

        for (var i = 0; i < before.Count; i++)
        {
            var expected = before[i].Id == id ? before[i] with { Still = after[i].Still, Take = after[i].Take } : before[i];
            if (expected != after[i])
            {
                return false;
            }
        }

        return true;
    }

    // The number Windows shows for the display ("\\.\DISPLAY2" → 2, trailing digits as on the filter
    // page), not the place in the list; 0 if the monitor is not attached.
    private static int DisplayNumber(string monitorId, List<string> attached)
    {
        var index = attached.IndexOf(monitorId);
        var start = monitorId.Length;
        while (start > 0 && char.IsAsciiDigit(monitorId[start - 1]))
        {
            start--;
        }

        return index < 0 ? 0
            : int.TryParse(monitorId.AsSpan(start), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number
            : index + 1;
    }
}
