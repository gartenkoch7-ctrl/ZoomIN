namespace ZoomIN.Core.WindowFilters;

/// <summary>Result of the measurement when a window opens (ADR-0020).</summary>
public enum WindowBrightness
{
    /// <summary>Still being measured; treated like dark until decided.</summary>
    Pending,

    Light,

    Dark,

    /// <summary>Could not be measured within 2 s (e.g. covered); treated like dark.</summary>
    Unknown,
}
