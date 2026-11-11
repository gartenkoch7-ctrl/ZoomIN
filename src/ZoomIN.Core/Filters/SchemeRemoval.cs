using ZoomIN.Core.Settings;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Filters;

/// <summary>
/// What happens when an own scheme is deleted (M18, decision): every place that chose it – the
/// shared and per-monitor filter, rules per window, the automatic filter, stored profiles – falls
/// back to <see cref="CustomSchemes.Fallback"/>. Each method counts every stored choice it changed,
/// also ones not showing right now (filter off, rule on "None"), so the page can show a hint and
/// telemetry a number.
/// </summary>
public static class SchemeRemoval
{
    /// <summary>Running effects without the scheme in <paramref name="id"/>.</summary>
    public static DisplayEffects Without(DisplayEffects effects, FilterId id, out int fallbacks)
    {
        ArgumentNullException.ThrowIfNull(effects);
        fallbacks = 0;
        var filter = effects.Filter;
        if (filter.Filter == id)
        {
            filter = filter with { Filter = CustomSchemes.Fallback };
            fallbacks++;
        }

        var monitors = Monitors(effects.MonitorFilters, id, ref fallbacks);
        return effects with
        {
            Filter = filter,
            MonitorFilters = monitors,
            Schemes = [.. effects.Schemes.Where(s => s.Id != id)],
        };
    }

    /// <summary>Rules per window and the automatic filter without the scheme in <paramref name="id"/>.</summary>
    public static WindowFilterOptions Without(WindowFilterOptions options, FilterId id, out int fallbacks)
    {
        ArgumentNullException.ThrowIfNull(options);
        fallbacks = 0;
        var rules = new List<WindowFilterRule>(options.Rules.Count);
        foreach (var rule in options.Rules)
        {
            var hit = rule.Filter == id;
            rules.Add(hit ? rule with { Filter = CustomSchemes.Fallback } : rule);
            fallbacks += hit ? 1 : 0;
        }

        var auto = options.AutoFilter;
        if (auto == id)
        {
            auto = CustomSchemes.Fallback;
            fallbacks++;
        }

        return options with { Rules = rules, AutoFilter = auto };
    }

    /// <summary>A stored profile whose choices of the scheme in <paramref name="id"/> fall back.</summary>
    public static AppSettings Without(AppSettings profile, FilterId id, out int fallbacks)
    {
        ArgumentNullException.ThrowIfNull(profile);
        fallbacks = 0;
        var filter = profile.ColorFilter;
        if (filter == id)
        {
            filter = CustomSchemes.Fallback;
            fallbacks++;
        }

        var monitors = Monitors(profile.MonitorFilters, id, ref fallbacks);
        return profile with
        {
            ColorFilter = filter,
            MonitorFilters = monitors,
        };
    }

    private static Dictionary<string, MonitorFilter> Monitors(IReadOnlyDictionary<string, MonitorFilter>? filters, FilterId id, ref int fallbacks)
    {
        var result = new Dictionary<string, MonitorFilter>(StringComparer.Ordinal);
        foreach (var (monitor, own) in filters ?? new Dictionary<string, MonitorFilter>())
        {
            if (own is null)
            {
                continue;
            }

            if (own.Filter == id)
            {
                result[monitor] = own with { Filter = CustomSchemes.Fallback };
                fallbacks++;
            }
            else
            {
                result[monitor] = own;
            }
        }

        return result;
    }
}
