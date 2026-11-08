using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Settings;

/// <summary>
/// Single owner of the settings (M7): shared settings (language, hotkeys, updates …) live here,
/// display settings live in the models. Every change is applied live and saved debounced –
/// to settings.json and to the active profile.
/// </summary>
public sealed class SettingsService : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    private readonly RenderModels _models;
    private readonly ISettingsStore _store;
    private readonly IProfileStore _profiles;
    private readonly Timer _saveTimer;
    private readonly Lock _gate = new();
    private AppSettings _shared;
    private bool _savedZoomOn;

    public SettingsService(AppSettings initial, RenderModels models, ISettingsStore store, IProfileStore profiles)
    {
        _shared = initial;
        _savedZoomOn = initial.ZoomOn;
        _models = models;
        _store = store;
        _profiles = profiles;
        _saveTimer = new Timer(_ => SaveNow());
        _models.Zoom.Changed += OnZoomChanged;
        _models.Effects.Changed += ScheduleSave;
        _models.Tracking.Changed += ScheduleSave;
        _models.Windows.OptionsChanged += ScheduleSave;
    }

    /// <summary>Raised after shared settings changed (language, hotkeys, …), on the caller's thread.</summary>
    public event Action? Changed;

    public RenderModels Models => _models;

    /// <summary>Everything as it would be saved now: shared settings plus the models' state.</summary>
    public AppSettings Current
    {
        get
        {
            AppSettings shared;
            lock (_gate)
            {
                shared = _shared;
            }

            var settings = ZoomMapping.Apply(shared, _models.Zoom);
            settings = EffectsMapping.Apply(settings, _models.Effects);
            settings = TrackingMapping.Apply(settings, _models.Tracking);

            // Shared by all profiles (ADR-0020): ApplyDisplay never takes it from a profile.
            return settings with { WindowFilter = _models.Windows.Options };
        }
    }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        lock (_gate)
        {
            _shared = change(_shared);
        }

        ScheduleSave();
        Changed?.Invoke();
    }

    /// <summary>Applies the display part of <paramref name="profile"/> to the running models.</summary>
    public void ApplyDisplay(AppSettings profile)
    {
        ZoomMapping.ApplyTo(_models.Zoom, profile);
        _models.Effects.Replace(EffectsMapping.Effects(profile));
        _models.Tracking.SetOptions(TrackingMapping.Options(profile));
    }

    public void SaveNow()
    {
        var settings = Current;
        Volatile.Write(ref _savedZoomOn, settings.ZoomOn);
        _store.Save(settings);
        _profiles.Save(settings.ActiveProfile, settings);
    }

    public void Dispose()
    {
        _models.Zoom.Changed -= OnZoomChanged;
        _models.Effects.Changed -= ScheduleSave;
        _models.Tracking.Changed -= ScheduleSave;
        _models.Windows.OptionsChanged -= ScheduleSave;
        _saveTimer.Dispose();
        SaveNow();
    }

    // The secure desktop starts with the saved on/off state (ADR-0016): Win+L right after
    // Strg+Alt+Z must not wait for the debounce.
    private void OnZoomChanged()
    {
        if (_models.Zoom.IsOn != Volatile.Read(ref _savedZoomOn))
        {
            SaveNow();
        }
        else
        {
            ScheduleSave();
        }
    }

    // Wheel zooming changes the level many times per second; write the files once it settles.
    private void ScheduleSave() => _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
}
