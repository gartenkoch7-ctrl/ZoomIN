using Microsoft.Extensions.Logging;
using ZoomIN.Core.Filters;

namespace ZoomIN.Rendering.TextEnhance;

/// <summary>
/// Logs when "Schärfe"/"Fett" turn on, off or pause because the smoothing is not text-optimised
/// (3200), and value changes while on (3201, debug: a slider drag sends many). Listens to settings
/// changes on the thread that made them, never on a render thread. With variant A (spike D1) no
/// pass or target is built for it: the magnify pass only binds another pixel shader.
/// </summary>
internal sealed class EnhanceWatch : IDisposable
{
    private readonly EffectsModel _effects;
    private readonly ILogger _log;
    private readonly Lock _gate = new();
    private string _state;
    private TextEnhancement _values;

    public EnhanceWatch(EffectsModel effects, ILogger log)
    {
        _effects = effects;
        _log = log;

        // Subscribed first and read under the lock: a change on another thread in between is then
        // either part of the start state or logged against it, never lost.
        lock (_gate)
        {
            _effects.Changed += OnChanged;
            var current = effects.Current;
            _values = current.Enhancement;
            _state = State(current);

            // The start state is worth one line only when the user turned it on.
            if (_state != "off")
            {
                Log(current);
            }
        }
    }

    public void Dispose() => _effects.Changed -= OnChanged;

    /// <summary>"on", "off" or "paused" (on, but the smoothing is "Weich" or "Pixel").</summary>
    public static string State(DisplayEffects effects) =>
        !effects.Enhancement.IsActive ? "off" : TextEnhancement.WorksWith(effects.Smoothing) ? "on" : "paused";

    private static int Percent(TextEnhancement values) => (int)Math.Round(values.Sharpness * 100);

    private static double Pixels(TextEnhancement values) => Math.Round(values.BoldPixels, 1);

    private void OnChanged()
    {
        // Under the lock, so two threads cannot log out of order.
        lock (_gate)
        {
            var current = _effects.Current;
            var state = State(current);
            var values = current.Enhancement;
            if (state != _state)
            {
                _state = state;
                _values = values;
                Log(current);
            }
            else if (values != _values)
            {
                _values = values;
                if (state != "off" && _log.IsEnabled(LogLevel.Debug))
                {
                    _log.ValuesChanged(Percent(values), Pixels(values));
                }
            }
        }
    }

    private void Log(DisplayEffects effects)
    {
        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.StateChanged(_state, Percent(effects.Enhancement), Pixels(effects.Enhancement), effects.Smoothing.ToString());
        }
    }
}
