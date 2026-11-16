using System.Globalization;

namespace ZoomIN.Watchdog;

/// <summary>
/// Command line of the watchdog mode: <c>ZoomIN.exe --watchdog &lt;pid&gt; [--secure]</c>; the
/// optional flag watches the secure-desktop instance (ADR-0016).
/// </summary>
public static class WatchdogArguments
{
    public const string Switch = "--watchdog";
    public const string SecureFlag = "--secure";

    public static string Format(int processId, bool secure = false) =>
        string.Create(CultureInfo.InvariantCulture, $"{Switch} {processId}{(secure ? " " + SecureFlag : "")}");

    public static bool TryParse(IReadOnlyList<string> args, out int processId, out bool secure)
    {
        processId = 0;
        secure = args.Count == 3 && args[2] == SecureFlag;
        return (args.Count == 2 || secure)
            && args[0] == Switch
            && int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out processId)
            && processId > 0;
    }
}
