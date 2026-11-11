namespace ZoomIN.Core.AgentDebug;

/// <summary>
/// The running ZoomIN's end of the KI-Debug link (ADR-0027). Only exists while the user allows it;
/// only the same user in the same session can connect.
/// </summary>
public interface IAgentDebugServer : IDisposable
{
    bool IsRunning { get; }

    /// <summary>Starts answering requests with <paramref name="handler"/>; no-op if it runs already.</summary>
    void Start(Func<AgentRequest, CancellationToken, Task<AgentResponse>> handler);

    /// <summary>Stops answering and removes the pipe; open requests are cancelled.</summary>
    void Stop();
}
