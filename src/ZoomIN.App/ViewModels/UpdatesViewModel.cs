using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Settings;
using ZoomIN.App.Updates;
using ZoomIN.Core.Updates;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Updates page (M9): channel, update check, version list and one-click install or downgrade.
/// Runs on the UI thread; long work is awaited, so the page stays usable with a screen reader.
/// </summary>
public sealed partial class UpdatesViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly UpdateService _updates;
    private readonly IShellActions _shell;
    private readonly bool _ready;
    private IReadOnlyList<ReleaseInfo> _offered = [];

    public UpdatesViewModel(SettingsService settings, UpdateService updates, IShellActions shell)
    {
        _settings = settings;
        _updates = updates;
        _shell = shell;
        var s = settings.Current;
        ChannelIndex = (int)s.UpdateChannel;
        CheckUpdates = s.CheckForUpdates;
        _ready = true;
        ShowVersions();
    }

    public string Installed => _updates.CurrentVersion.ToString();

    public bool NotInstallable => !_updates.CanInstall;

    public ObservableCollection<string> Versions { get; } = [];

    [ObservableProperty]
    public partial int ChannelIndex { get; set; }

    [ObservableProperty]
    public partial bool CheckUpdates { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallText), nameof(CanInstallSelected), nameof(IsDowngrade))]
    public partial int SelectedIndex { get; set; } = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallSelected), nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double Progress { get; set; }

    public bool IsIdle => !IsBusy;

    public bool IsDowngrade => Selected is { } r && Kind(r) == UpdateKind.Downgrade;

    public bool CanInstallSelected => !IsBusy && _updates.CanInstall && Selected is { } r && Kind(r) != UpdateKind.Reinstall;

    public string InstallText => Selected is not { } r
        ? Text.Format("Updates.Upgrade", "…")
        : Kind(r) switch
        {
            UpdateKind.Upgrade => Text.Format("Updates.Upgrade", r.Version),
            UpdateKind.Downgrade => Text.Format("Updates.Downgrade", r.Version),
            _ => Text.Format("Updates.Current", r.Version),
        };

    private ReleaseInfo? Selected => SelectedIndex >= 0 && SelectedIndex < _offered.Count ? _offered[SelectedIndex] : null;

    public async Task RefreshAsync()
    {
        IsBusy = true;
        Status = Text.Get("Updates.Loading");
        await _updates.RefreshAsync(CancellationToken.None);
        IsBusy = false;
        ShowVersions();
    }

    public async Task InstallAsync()
    {
        if (Selected is not { } release)
        {
            return;
        }

        IsBusy = true;
        Progress = 0;
        Status = Text.Format("Updates.Downloading", release.Version);
        var progress = new Progress<double>(value =>
        {
            Progress = value * 100;
            if (value >= 1)
            {
                Status = Text.Format("Updates.Installing", release.Version);
            }
        });

        var result = await _updates.InstallAsync(release, progress, CancellationToken.None);
        Status = Text.Format("Updates.Result." + result, release.Version);
        if (result == UpdateResult.Installed)
        {
            _shell.ExitForUpdate();
            return;
        }

        IsBusy = false;
    }

    partial void OnChannelIndexChanged(int value)
    {
        if (_ready && value >= 0)
        {
            _settings.Update(s => s with { UpdateChannel = (ReleaseChannel)value });
            ShowVersions();
        }
    }

    partial void OnCheckUpdatesChanged(bool value)
    {
        if (_ready)
        {
            _settings.Update(s => s with { CheckForUpdates = value });
        }
    }

    private void ShowVersions()
    {
        _offered = _updates.Offered;
        Versions.Clear();
        foreach (var release in _offered)
        {
            Versions.Add(Label(release));
        }

        var update = _updates.AvailableUpdate;
        SelectedIndex = update is not null ? IndexOf(update) : _offered.Count > 0 ? 0 : -1;
        Status = _offered.Count == 0 ? Text.Get("Updates.None")
            : update is not null ? Text.Format("Updates.Available", update.Version)
            : Text.Get("Updates.UpToDate");
    }

    private int IndexOf(ReleaseInfo release)
    {
        for (var i = 0; i < _offered.Count; i++)
        {
            if (_offered[i] == release)
            {
                return i;
            }
        }

        return -1;
    }

    private UpdateKind Kind(ReleaseInfo release) => ReleaseCatalog.Classify(_updates.CurrentVersion, release.Version);

    private static string Label(ReleaseInfo release)
    {
        var date = release.PublishedAt.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
        return release.Version.IsPreRelease
            ? $"{release.Version} – {date} ({Text.Get("Updates.PreRelease")})"
            : $"{release.Version} – {date}";
    }
}
