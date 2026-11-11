namespace ZoomIN.Core.AgentDebug;

/// <summary>The MCP role's end of the KI-Debug link (ADR-0027).</summary>
public interface IAgentDebugClient
{
    /// <summary>
    /// Sends one request to the running ZoomIN. Returns a failed <see cref="AgentResponse"/> (no
    /// exception) if ZoomIN does not run, KI-Debug is off or the answer does not come in time.
    /// </summary>
    Task<AgentResponse> SendAsync(AgentRequest request, CancellationToken cancellationToken);
}
