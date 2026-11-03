using System.Diagnostics;
using System.Globalization;

namespace ZoomIN.App.Updates;

/// <summary>
/// "ZoomIN.exe --wait-for &lt;pid&gt;": the new version started after an update waits until the old
/// one has exited, otherwise the single-instance check would end it at once (ADR-0015).
/// </summary>
internal static class RestartArguments
{
    public const string Switch = "--wait-for";

    public static string Format(int processId) => string.Create(CultureInfo.InvariantCulture, $"{Switch} {processId}");

    public static void WaitIfRequested(IReadOnlyList<string> args)
    {
        if (args.Count != 2 || args[0] != Switch ||
            !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit(TimeSpan.FromSeconds(15));
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
    }
}
