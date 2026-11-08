using System.Globalization;

namespace ZoomIN.App.Updates;

/// <summary>
/// "ZoomIN.exe --wait-for &lt;pid&gt;": the new version started after an update waits until the old
/// one has exited, otherwise the single-instance check would end it at once (ADR-0015). Only the
/// argument format; the waiting is done by <see cref="Program"/>.
/// </summary>
internal static class RestartArguments
{
    public const string Switch = "--wait-for";

    public static string Format(int processId) => string.Create(CultureInfo.InvariantCulture, $"{Switch} {processId}");

    /// <summary>True if <paramref name="args"/> ask to wait for the process <paramref name="processId"/>.</summary>
    public static bool TryParse(IReadOnlyList<string> args, out int processId)
    {
        processId = 0;
        return args.Count == 2 && args[0] == Switch
            && int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out processId);
    }
}
