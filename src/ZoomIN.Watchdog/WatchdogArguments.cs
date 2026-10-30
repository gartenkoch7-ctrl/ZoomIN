using System.Globalization;

namespace ZoomIN.Watchdog;

/// <summary>Command line of the watchdog mode: <c>ZoomIN.exe --watchdog &lt;pid&gt;</c>.</summary>
public static class WatchdogArguments
{
    public const string Switch = "--watchdog";

    public static string Format(int processId) =>
        string.Create(CultureInfo.InvariantCulture, $"{Switch} {processId}");

    public static bool TryParse(IReadOnlyList<string> args, out int processId)
    {
        processId = 0;
        return args.Count == 2
            && args[0] == Switch
            && int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out processId)
            && processId > 0;
    }
}
