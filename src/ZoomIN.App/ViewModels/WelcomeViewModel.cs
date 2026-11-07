using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// First-run assistant (M7): try the zoom, Strg+Mausrad note, colours, autostart (preselected:
/// yes), update check (opt-in: preselected no). Shown until it is finished once.
/// </summary>
public sealed partial class WelcomeViewModel : ObservableObject
{
    public const int StepCount = 5;
    private readonly SettingsService _settings;
    private readonly IAutostart _autostart;
    private readonly ZoomCommands _zoom;
    private readonly IShellActions _shell;

    public WelcomeViewModel(SettingsService settings, IAutostart autostart, ZoomCommands zoom, IShellActions shell)
    {
        _settings = settings;
        _autostart = autostart;
        _zoom = zoom;
        _shell = shell;
        Autostart = true;
        CheckUpdates = true;
        ShowStep();
    }

    [ObservableProperty]
    public partial int Step { get; set; }

    [ObservableProperty]
    public partial string StepTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StepText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Autostart { get; set; }

    [ObservableProperty]
    public partial bool CheckUpdates { get; set; }

    public bool CanGoBack => Step > 0;

    public bool IsLastStep => Step == StepCount - 1;

    public bool ShowsFilterToggle => Step == 2;

    public bool ShowsAutostart => Step == 3;

    public bool ShowsUpdates => Step == 4;

    public string NextText => IsLastStep ? Text.Get("WelcomeFinish") : Text.Get("WelcomeNext");

    public string Progress => $"{Step + 1} / {StepCount}";

    public void Back()
    {
        if (CanGoBack)
        {
            Step--;
            ShowStep();
        }
    }

    public void Next()
    {
        if (!IsLastStep)
        {
            Step++;
            ShowStep();
            return;
        }

        _autostart.SetEnabled(Autostart);
        _settings.Update(s => s with { CheckForUpdates = CheckUpdates, FirstRunCompleted = true });
        _shell.ShowOverview();
    }

    public void ToggleFilter() => _zoom.ToggleFilter();

    private void ShowStep()
    {
        StepTitle = Text.Get($"Welcome.{Step + 1}.Title");
        StepText = Text.Get($"Welcome.{Step + 1}.Text");
        OnPropertyChanged(string.Empty);
    }
}
