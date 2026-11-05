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
    public bool Equals(WindowKind other) =>
        string.Equals(Process, other.Process, StringComparison.OrdinalIgnoreCase)
        && string.Equals(WindowClass, other.WindowClass, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(Process ?? string.Empty),
        StringComparer.OrdinalIgnoreCase.GetHashCode(WindowClass ?? string.Empty));
}
