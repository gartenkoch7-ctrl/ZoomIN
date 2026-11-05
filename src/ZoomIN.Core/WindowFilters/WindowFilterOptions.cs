using ZoomIN.Core.Filters;

namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// Settings of the filter per window (ADR-0020), shared by all profiles. Properties use <c>set</c>
/// for the JSON source generator (see the remark on AppSettings); treat instances as immutable.
/// </summary>
public sealed record WindowFilterOptions
{
    public const int MaxRules = 500;
    public const int MinPercent = 20;
    public const int MaxPercent = 95;

    public static WindowFilterOptions Default { get; } = new();

    /// <summary>On from the start (decision), also without zoom.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Filter for windows measured as light.</summary>
    public ColorFilter AutoFilter { get; set; } = ColorFilter.Inverted;

    public WindowFilterSensitivity Sensitivity { get; set; } = WindowFilterSensitivity.Medium;

    /// <summary>Share of light area (in %) from which a window counts as light; used with <see cref="WindowFilterSensitivity.Custom"/>.</summary>
    public int CustomPercent { get; set; } = 60;

    public IReadOnlyList<WindowFilterRule> Rules { get; set; } = [];

    /// <summary>Light share in % from which a window counts as light. Higher sensitivity = lower share.</summary>
    public int ThresholdPercent => Sensitivity switch
    {
        WindowFilterSensitivity.Low => 75,
        WindowFilterSensitivity.High => 45,
        WindowFilterSensitivity.Custom => Math.Clamp(CustomPercent, MinPercent, MaxPercent),
        _ => 60,
    };

    public WindowFilterRule? RuleFor(WindowKind kind)
    {
        foreach (var rule in Rules)
        {
            if (rule.Kind.Equals(kind))
            {
                return rule;
            }
        }

        return null;
    }

    /// <summary>Adds or replaces the rule for its app + window class; the newest comes first.</summary>
    public WindowFilterOptions WithRule(WindowFilterRule rule)
    {
        var rules = new List<WindowFilterRule>(Rules.Count + 1) { rule };
        foreach (var existing in Rules)
        {
            if (!existing.Kind.Equals(rule.Kind) && rules.Count < MaxRules)
            {
                rules.Add(existing);
            }
        }

        return this with { Rules = rules };
    }

    public WindowFilterOptions WithoutRule(WindowKind kind) =>
        this with { Rules = Rules.Where(r => !r.Kind.Equals(kind)).ToList() };

    /// <summary>Drops broken entries (empty names, unknown values, duplicates) and bounds the numbers.</summary>
    public WindowFilterOptions Normalized()
    {
        var seen = new HashSet<WindowKind>();
        var rules = new List<WindowFilterRule>();
        foreach (var rule in Rules ?? [])
        {
            if (rule is null || !IsName(rule.Process) || !IsName(rule.WindowClass)
                || !Enum.IsDefined(rule.Choice) || !Enum.IsDefined(rule.Filter) || !seen.Add(rule.Kind)
                || rules.Count >= MaxRules)
            {
                continue;
            }

            rules.Add(rule);
        }

        return this with
        {
            AutoFilter = Enum.IsDefined(AutoFilter) ? AutoFilter : ColorFilter.Inverted,
            Sensitivity = Enum.IsDefined(Sensitivity) ? Sensitivity : WindowFilterSensitivity.Medium,
            CustomPercent = Math.Clamp(CustomPercent, MinPercent, MaxPercent),
            Rules = rules,
        };
    }

    // Window class names are at most 256 characters (RegisterClass); file names stay below that too.
    private static bool IsName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 256;
}
