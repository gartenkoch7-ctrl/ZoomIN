namespace ZoomIN.Core.Monitors;

/// <summary>Readable parts of a monitor id (the Windows device name, e.g. "\\.\DISPLAY2").</summary>
public static class MonitorNames
{
    /// <summary>
    /// The number Windows shows under Settings › Display ("\\.\DISPLAY2" → 2), for announcements
    /// such as "Monitor 2 frozen"; 0 if the id does not end with one.
    /// </summary>
    public static int Number(string monitorId)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        var start = monitorId.Length;
        while (start > 0 && char.IsAsciiDigit(monitorId[start - 1]))
        {
            start--;
        }

        return start < monitorId.Length && int.TryParse(monitorId.AsSpan(start), out var number) ? number : 0;
    }
}
