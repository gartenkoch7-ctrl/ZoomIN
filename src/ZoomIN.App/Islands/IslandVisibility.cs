using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Shell;

namespace ZoomIN.App.Islands;

/// <summary>Ctrl+Alt+I and the switch "Inseln anzeigen" (M19 B1): shows or hides all islands, with announcement.</summary>
public sealed class IslandVisibility(SettingsService settings, IAnnouncer announcer, ILocalizer texts, ILogger<IslandVisibility> log)
{
    private readonly IslandModel _model = settings.Models.Islands;
    private readonly IslandReport _report = new(announcer, texts, log);

    /// <summary>The shortcut: the other state, in one step.</summary>
    public void Toggle() => Report(_model.ToggleVisible());

    /// <summary>The switch: exactly the state it shows (a shortcut at the same moment must not invert it).</summary>
    public void Set(bool visible)
    {
        // Reported only on a real change: a shortcut at the same moment may have done it already.
        if (_model.SetVisible(visible))
        {
            Report(visible);
        }
    }

    private void Report(bool visible)
    {
        var count = _model.Scene.Set.Count;
        IslandOperation.Toggled(visible, count);
        log.IslandsToggled(visible, count);
        _report.Say(visible ? "Islands.On" : "Islands.Off");
    }
}
