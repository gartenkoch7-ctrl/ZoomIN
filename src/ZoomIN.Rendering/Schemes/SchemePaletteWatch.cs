using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;

namespace ZoomIN.Rendering.Schemes;

/// <summary>
/// Logs when the own schemes change what the shader gets (M18): the palette entries of the places
/// and whether the colour replacement step runs. Listens to settings changes on the thread that
/// made them, never on a render thread; only kind and place, no names (AGENTS.md 2.5).
/// </summary>
internal sealed class SchemePaletteWatch : IDisposable
{
    private static readonly Gauge<int> s_count = Telemetry.Meter.CreateGauge<int>(
        "zoomin.filters.custom_schemes", "{schemes}", "Own colour schemes defined (M18)");

    private readonly EffectsModel _effects;
    private readonly ILogger _log;
    private readonly Lock _gate = new();
    private IReadOnlyList<CustomScheme> _schemes;
    private int _layout;
    private int _replacements;

    public SchemePaletteWatch(EffectsModel effects, ILogger log)
    {
        _effects = effects;
        _log = log;
        var current = effects.Current;
        _schemes = current.Schemes;
        _layout = Layout(current.Schemes);
        _replacements = Replacements(current.Replacements);
        s_count.Record(current.Schemes.Count);
        _effects.Changed += OnChanged;
    }

    public void Dispose() => _effects.Changed -= OnChanged;

    /// <summary>"Custom1 Tint, Custom3 Deficiency": what each place holds.</summary>
    public static string Places(IReadOnlyList<CustomScheme> schemes) =>
        string.Join(", ", schemes.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.Id} {s.Kind}")));

    // Kind + 1 of each place in 3 bits (0 = empty): exact, and no strings built on every edit.
    private static int Layout(IReadOnlyList<CustomScheme> schemes)
    {
        var layout = 0;
        foreach (var scheme in schemes)
        {
            layout |= ((int)scheme.Kind + 1) << (3 * CustomSchemes.SlotOf(scheme.Id));
        }

        return layout;
    }

    // Replacements of each place in 2 bits (0–3), so a move between schemes counts as a change.
    private static int Replacements(ReplacementTable table)
    {
        var counts = 0;
        for (var slot = 0; slot < CustomSchemes.Max; slot++)
        {
            counts |= table.CountOf(slot) << (2 * slot);
        }

        return counts;
    }

    private static int Total(int counts)
    {
        var total = 0;
        for (var slot = 0; slot < CustomSchemes.Max; slot++)
        {
            total += (counts >> (2 * slot)) & 3;
        }

        return total;
    }

    private void OnChanged()
    {
        // Most changes (sliders, shortcuts) keep the scheme list: with copies share it, so skip at once.
        // All under the lock, so two threads cannot log out of order.
        lock (_gate)
        {
            var current = _effects.Current;
            if (ReferenceEquals(current.Schemes, _schemes))
            {
                return;
            }

            _schemes = current.Schemes;
            var layout = Layout(current.Schemes);
            var replacements = Replacements(current.Replacements);
            if (layout != _layout)
            {
                _layout = layout;
                var places = Places(current.Schemes);
                s_count.Record(current.Schemes.Count);
                _log.PaletteRebuilt(current.Schemes.Count, places.Length > 0 ? places : "none");
            }

            if (replacements != _replacements)
            {
                var wasActive = _replacements > 0;
                _replacements = replacements;
                var total = Total(replacements);
                _log.ReplaceStep(replacements > 0 ? (wasActive ? "changed" : "on") : "off", total);
            }
        }
    }
}
