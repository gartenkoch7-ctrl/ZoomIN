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

    public void Toggle()
    {
        var visible = !_model.Scene.Visible;
        _model.SetVisible(visible);
        var count = _model.Scene.Set.Count;
        IslandOperation.Toggled(visible, count);
        log.IslandsToggled(visible, count);
        _report.Say(visible ? "Islands.On" : "Islands.Off");
    }
}
