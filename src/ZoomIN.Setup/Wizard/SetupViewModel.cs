using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Updates;
using ZoomIN.Core.Zoom;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Updates;
using ZoomIN.Setup.Install;
using ZoomIN.Setup.Localization;

namespace ZoomIN.Setup.Wizard;

/// <summary>
/// Wizard state (M9): 0 welcome/language, 1 channel/version, 2 settings, 3 install, 4 done.
/// Every step has a title and a text that screen readers announce when it appears.
/// </summary>
public sealed partial class SetupViewModel : ObservableObject
{
    public const int StepCount = 5;

    // Index in the colour filter list; 0 = off.
    private static readonly ColorFilter?[] s_filters =
        [null, ColorFilter.Inverted, ColorFilter.Grayscale, ColorFilter.YellowOnBlack, ColorFilter.BlackOnYellow, ColorFilter.WhiteOnBlue];

    private readonly FileLog _log = new();
    private readonly GitHubReleaseFeed _feed;
    private IReadOnlyList<ReleaseInfo> _releases = [];
    private IReadOnlyList<ReleaseInfo> _offered = [];

    public SetupViewModel()
    {
        _feed = new GitHubReleaseFeed(_log);
        LanguageIndex = SetupText.IsGerman ? 0 : 1;
        InstallSteps.StatusChanged += text => Status = text;
    }

    /// <summary>Step 3: what the installation does, ticked as it goes.</summary>
    public InstallStepList InstallSteps { get; } = new();

    /// <summary>Raised after a language switch; the window re-evaluates all texts.</summary>
    public event Action? TextsChanged;

    public ObservableCollection<string> Versions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(CanGoNext), nameof(NextText), nameof(ProgressText), nameof(StepTitle), nameof(StepText))]
    public partial int Step { get; set; }

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    [ObservableProperty]
    public partial int ChannelIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    public partial int SelectedIndex { get; set; } = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(CanGoNext))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial bool Failed { get; set; }

    public bool Autostart { get; set; } = true;

    public bool DesktopShortcut { get; set; } = true;

    public bool CheckForUpdates { get; set; } = true;

    public bool SecureDesktop { get; set; } = true;

    public double ZoomLevel { get; set; } = ZoomLimits.DefaultLevel;

    public int FilterIndex { get; set; }

    public bool StartNow { get; set; } = true;

    public string StepTitle => SetupText.Get($"Step{Step}.Title");

    public string StepText => Step == 4 ? SetupText.Format("Step4.Text", Selected?.Version.ToString() ?? string.Empty) : SetupText.Get($"Step{Step}.Text");

    public string ProgressText => SetupText.Format("Progress", Step + 1, StepCount);

    public bool CanGoBack => !IsBusy && Step is 1 or 2 || (Step == 3 && Failed && !IsBusy);

    public bool CanGoNext => !IsBusy && Step switch
    {
        1 => Selected is not null,
        3 => Failed,
        _ => true,
    };

    public string NextText => Step switch
    {
        2 => SetupText.Get("Install"),
        3 => SetupText.Get("Retry"),
        4 => SetupText.Get("Finish"),
        _ => SetupText.Get("Next"),
    };

    private ReleaseInfo? Selected => SelectedIndex >= 0 && SelectedIndex < _offered.Count ? _offered[SelectedIndex] : null;

    public void Back() => Step = Step == 3 ? 2 : Math.Max(0, Step - 1);

    /// <returns>True when the wizard is finished and the window may close.</returns>
    public async Task<bool> NextAsync()
    {
        switch (Step)
        {
            case 0:
                Step = 1;
                if (_releases.Count == 0)
                {
                    await RefreshAsync();
                }

                return false;
            case 2:
            case 3:
                Step = 3;
                await InstallAsync();
                return false;
            case 4:
                if (StartNow)
                {
                    SetupInstaller.StartZoomIN();
                }

                return true;
            default:
                Step++;
                return false;
        }
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        Status = SetupText.Get("Loading");
        _releases = await _feed.ReleasesAsync(ReleaseFeeds.GitHub, CancellationToken.None);
        IsBusy = false;
        ShowVersions();
    }

    partial void OnLanguageIndexChanged(int value)
    {
        SetupText.SetGerman(value == 0);
        ShowVersions();
        InstallSteps.Refresh();
        TextsChanged?.Invoke();
    }

    partial void OnChannelIndexChanged(int value) => ShowVersions();

    private async Task InstallAsync()
    {
        if (Selected is not { } release)
        {
            return;
        }

        IsBusy = true;
        Failed = false;
        Progress = 0;
        var choices = new SetupChoices((ReleaseChannel)ChannelIndex, Autostart, DesktopShortcut, CheckForUpdates, ZoomLevel, s_filters[FilterIndex], SecureDesktop);
        InstallSteps.Start(release.Version.ToString());
        var result = await new SetupInstaller(_log).InstallAsync(
            release, choices, InstallSteps.Checklist, new Progress<double>(value => Progress = value * 100), CancellationToken.None);
        IsBusy = false;
        if (result == UpdateResult.Installed)
        {
            Step = 4;
            return;
        }

        InstallSteps.Checklist.Finish(succeeded: false);
        Failed = true;
        Status = SetupText.Get("Result." + (result is UpdateResult.PackageMismatch or UpdateResult.SignatureRejected or UpdateResult.Declined ? result : UpdateResult.Failed));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
    }

    private void ShowVersions()
    {
        if (_releases.Count == 0 && IsBusy)
        {
            return;
        }

        _offered = ReleaseCatalog.Offered(_releases, (ReleaseChannel)ChannelIndex);
        Versions.Clear();
        foreach (var release in _offered)
        {
            var date = release.PublishedAt.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
            Versions.Add(release.Version.IsPreRelease ? $"{release.Version} – {date} ({SetupText.Get("PreRelease")})" : $"{release.Version} – {date}");
        }

        SelectedIndex = _offered.Count > 0 ? 0 : -1;
        Status = _offered.Count == 0 ? SetupText.Get("None") : SetupText.Format("Found", _offered.Count);
    }
}
