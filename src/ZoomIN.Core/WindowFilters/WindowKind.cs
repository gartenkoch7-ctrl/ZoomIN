namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// App + window class (ADR-0020): what a user choice is remembered for. The title is never part of
/// it (changes all the time, and is user content). Compared case-insensitively, like Windows does
/// for file and class names.
/// </summary>
/// <param name="Process">File name of the program, e.g. "winword.exe" (no path).</param>
/// <param name="WindowClass">Win32 window class, e.g. "OpusApp".</param>
public readonly record struct WindowKind(string Process, string WindowClass)
{
    /// <summary>Process name when the program file cannot be read (protected or gone).</summary>
    public const string UnknownProcess = "unknown";

    /// <summary>
    /// A user choice can be remembered for it. Not for unreadable programs (one name for many
    /// apps) and not for the store app frame before its app is known (one frame for all store apps).
    /// </summary>
    public bool CanHaveRule =>
        !string.Equals(Process, UnknownProcess, StringComparison.OrdinalIgnoreCase) && !IsStoreFrame;

    /// <summary>The frame of a store app whose app window was not found yet (ApplicationFrameHost).</summary>
    public bool IsStoreFrame =>
        string.Equals(Process, "applicationframehost.exe", StringComparison.OrdinalIgnoreCase)
        && string.Equals(WindowClass, "ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase);

    public bool Equals(WindowKind other) =>
        string.Equals(Process, other.Process, StringComparison.OrdinalIgnoreCase)
        && string.Equals(WindowClass, other.WindowClass, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(Process ?? string.Empty),
        StringComparer.OrdinalIgnoreCase.GetHashCode(WindowClass ?? string.Empty));
}
