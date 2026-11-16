namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// No user content in telemetry (ADR-0019): process name, window class, control type, rectangles,
/// file names of ZoomIN's own files and technical data are fine; window titles, element names,
/// text, full paths and addresses are not. Exporters drop attributes this rejects, and an
/// architecture test checks every attribute key and log placeholder in the code.
/// </summary>
public static class TelemetryPrivacy
{
    private static readonly string[] s_forbiddenParts = ["title", "text", "content", "document", "clipboard"];

    // Judged by the last segment, so "zoomin.update.path" and a placeholder "{Path}" both fail.
    private static readonly string[] s_forbiddenLastSegments = ["path", "url", "uri", "fullpath"];

    private static readonly string[] s_forbiddenEnds = ["element.name", "window.name"];

    public static bool IsAllowedAttribute(string key)
    {
        foreach (var part in s_forbiddenParts)
        {
            if (key.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var last = key[(key.LastIndexOf('.') + 1)..];
        foreach (var segment in s_forbiddenLastSegments)
        {
            if (last.Equals(segment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var end in s_forbiddenEnds)
        {
            if (key.EndsWith(end, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
