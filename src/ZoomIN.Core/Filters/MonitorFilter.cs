namespace ZoomIN.Core.Filters;

/// <summary>
/// The part of the colour filter a monitor can have on its own: on/off and which filter.
/// Contrast and brightness stay shared by all monitors (per-monitor filter decision).
/// </summary>
public sealed record MonitorFilter(bool Enabled, FilterId Filter);
