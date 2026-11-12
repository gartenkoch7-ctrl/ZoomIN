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

    /// <summary>
    /// Revision of the factory value of <see cref="AutoFilter"/>. 1 = smart inversion (ADR-0024).
    /// Settings written before have 0 (missing in the JSON), so the old factory value can be told apart.
    /// </summary>
    public const int CurrentAutoFilterRevision = 1;

    public static WindowFilterOptions Default { get; } = new() { AutoFilterRevision = CurrentAutoFilterRevision };

    /// <summary>On from the start (decision), also without zoom.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Filter for windows measured as light; from the factory the smart inversion (ADR-0024).</summary>
    public FilterId AutoFilter { get; set; } = FilterId.InvertedSmart;

    /// <summary>Which factory value <see cref="AutoFilter"/> was last checked against; 0 in older settings.</summary>
    public int AutoFilterRevision { get; set; }

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

    /// <summary>
    /// Drops broken entries (empty names, unknown values, duplicates) and bounds the numbers. Moves
    /// the old factory value "Inverted" to the smart inversion once (ADR-0024, decision 7); a user
    /// who picks "Inverted" afterwards keeps it, because the revision is stored with it.
    /// </summary>
    public WindowFilterOptions Normalized()
    {
        var seen = new HashSet<WindowKind>();
        var rules = new List<WindowFilterRule>();
        foreach (var rule in Rules ?? [])
        {
            if (rule is null || !IsName(rule.Process) || !IsName(rule.WindowClass)
                || !Enum.IsDefined(rule.Choice) || !Enum.IsDefined(rule.Filter)
                || !Enum.IsDefined(rule.TitleBar) || !seen.Add(rule.Kind)
                || rules.Count >= MaxRules)
            {
                continue;
            }

            rules.Add(rule);
        }

        var autoFilter = !Enum.IsDefined(AutoFilter) ? FilterId.InvertedSmart
            : AutoFilterRevision < CurrentAutoFilterRevision && AutoFilter == FilterId.Inverted ? FilterId.InvertedSmart
            : AutoFilter;
        return this with
        {
            AutoFilter = autoFilter,
            AutoFilterRevision = CurrentAutoFilterRevision,
            Sensitivity = Enum.IsDefined(Sensitivity) ? Sensitivity : WindowFilterSensitivity.Medium,
            CustomPercent = Math.Clamp(CustomPercent, MinPercent, MaxPercent),
            Rules = rules,
        };
    }

    // Window class names are at most 256 characters (RegisterClass); file names stay below that too.
    private static bool IsName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 256;
}
