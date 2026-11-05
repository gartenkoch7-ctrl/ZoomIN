namespace ZoomIN.Core.WindowFilters;

/// <summary>What a window gets (ADR-0020): measured, never filtered, or a fixed filter.</summary>
public enum WindowFilterChoice
{
    /// <summary>Light windows get the automatic filter, dark ones the monitor filter.</summary>
    Automatic,

    /// <summary>Original colours, also while the monitor is filtered.</summary>
    None,

    /// <summary>Always the filter of the rule, instead of the monitor filter (never both).</summary>
    Filter,
}
