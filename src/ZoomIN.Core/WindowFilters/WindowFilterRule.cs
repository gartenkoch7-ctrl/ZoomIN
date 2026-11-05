using ZoomIN.Core.Filters;

namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// A user choice for one app + window class, stored in the settings. Properties use <c>set</c>
/// for the JSON source generator (see the remark on AppSettings).
/// </summary>
public sealed record WindowFilterRule
{
    public string Process { get; set; } = string.Empty;

    public string WindowClass { get; set; } = string.Empty;

    public WindowFilterChoice Choice { get; set; }

    /// <summary>Used with <see cref="WindowFilterChoice.Filter"/>.</summary>
    public ColorFilter Filter { get; set; } = ColorFilter.Inverted;

    public WindowKind Kind => new(Process, WindowClass);

    public static WindowFilterRule For(WindowKind kind, WindowFilterChoice choice, ColorFilter filter = ColorFilter.Inverted) =>
        new() { Process = kind.Process, WindowClass = kind.WindowClass, Choice = choice, Filter = filter };
}
