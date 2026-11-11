using System.Globalization;

namespace ZoomIN.Core.AgentDebug;

/// <summary>Constants of the pipe between the MCP role and the running ZoomIN (ADR-0027).</summary>
public static class AgentProtocol
{
    /// <summary>Raised on incompatible changes; both sides come from the same exe, but an update may be half done.</summary>
    public const int Version = 1;

    /// <summary>Upper limit of one frame. Snapshots of several large monitors stay well below it.</summary>
    public const int MaxFrameBytes = 256 * 1024 * 1024;

    /// <summary>One pipe per Windows session, so two signed-in users never meet.</summary>
    public static string PipeName(int sessionId) =>
        string.Create(CultureInfo.InvariantCulture, $"ZoomIN.AgentDebug.{sessionId}");
}
