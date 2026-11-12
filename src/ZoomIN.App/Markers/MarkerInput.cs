using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Markers;

/// <summary>
/// Keeps the hold key hook in step with the show conditions of the highlights (E1): it runs only
/// while a switched-on crosshair, caret marker or focus frame waits for the hold key, and watches
/// the key from the shared settings. Logs when a condition changes (never per key).
/// </summary>
public sealed class MarkerInput : IDisposable
{
    private readonly SettingsService _settings;
    private readonly IHoldKeySource _hook;
    private readonly ILogger _log;
    private readonly Lock _gate = new();
    private (ShowCondition Crosshair, ShowCondition Caret, ShowCondition Focus) _conditions;
    private bool _started;
    private bool _hookNeeded;
    private bool _hookRunning;

    public MarkerInput(SettingsService settings, IHoldKeySource hook, ILogger<MarkerInput> log)
    {
        _settings = settings;
        _hook = hook;
        _log = log;
    }

    private MarkerActivity Activity => _settings.Models.Markers;

    public void Start()
    {
        var models = _settings.Models;
        models.Effects.Changed += OnModelsChanged;
        models.Tracking.Changed += OnModelsChanged;
        _settings.Changed += OnSettingsChanged;
        lock (_gate)
        {
            _started = true;
            _conditions = Conditions();
            Activity.Key = _settings.Current.MarkerHoldKey;
            ApplyHook(conditionsChanged: true);
        }
    }

    public void Dispose()
    {
        var models = _settings.Models;
        models.Effects.Changed -= OnModelsChanged;
        models.Tracking.Changed -= OnModelsChanged;
        _settings.Changed -= OnSettingsChanged;
        lock (_gate)
        {
            _started = false;
            _hookNeeded = false;
            _hookRunning = false;
            _hook.Stop();
        }
    }

    // Model events come from the UI, hotkey and hook threads; the lock keeps Start/Stop in order.
    private void OnModelsChanged()
    {
        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            var conditions = Conditions();
            LogChange("crosshair", _conditions.Crosshair, conditions.Crosshair);
            LogChange("caret", _conditions.Caret, conditions.Caret);
            LogChange("focus_frame", _conditions.Focus, conditions.Focus);
            var changed = conditions != _conditions;
            _conditions = conditions;
            ApplyHook(changed);
        }
    }

    // The hold key is a shared setting: it changes only here, not with the display models.
    private void OnSettingsChanged()
    {
        lock (_gate)
        {
            var key = _settings.Current.MarkerHoldKey;
            if (!_started || key == Activity.Key)
            {
                return;
            }

            Activity.Key = key;
            _log.HoldKeyChanged(key);

            // A new key is also a chance to install a hook Windows refused before.
            ApplyHook(conditionsChanged: true);
        }
    }

    private void ApplyHook(bool conditionsChanged)
    {
        // A hook Windows refused is tried again when a condition changes, not on every slider step.
        var models = _settings.Models;
        var needed = MarkerVisibility.NeedsHoldKey(models.Effects.Current.Cursor, models.Tracking.Options);
        if (needed && !_hookRunning && (!_hookNeeded || conditionsChanged))
        {
            _hookRunning = _hook.Start(Activity);
        }
        else if (!needed && _hookRunning)
        {
            _hook.Stop();
            _hookRunning = false;
        }

        _hookNeeded = needed;
    }

    private (ShowCondition Crosshair, ShowCondition Caret, ShowCondition Focus) Conditions()
    {
        var models = _settings.Models;
        var tracking = models.Tracking.Options;
        return (models.Effects.Current.Cursor.CrosshairShow, tracking.CaretShow, tracking.FocusFrameShow);
    }

    private void LogChange(string marker, ShowCondition before, ShowCondition after)
    {
        if (before != after)
        {
            _log.ShowConditionChanged(marker, after);
        }
    }
}
