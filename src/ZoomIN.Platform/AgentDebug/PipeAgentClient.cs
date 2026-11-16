using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.AgentDebug;

namespace ZoomIN.Platform.AgentDebug;

/// <summary>The MCP role's end of the KI-Debug link (ADR-0027): one connection per request.</summary>
public sealed class PipeAgentClient(ILogger<PipeAgentClient> log, string pipeName) : IAgentDebugClient
{
    public PipeAgentClient(ILogger<PipeAgentClient> log)
        : this(log, PipeAgentServer.SessionPipeName())
    {
    }

    private static readonly TimeSpan s_connectTimeout = TimeSpan.FromSeconds(2);

    // Snapshots wait for the renderers and encode PNGs; everything else answers at once.
    private static readonly TimeSpan s_answerTimeout = TimeSpan.FromSeconds(30);

    public const string NotReachableText =
        "ZoomIN is not reachable. Either ZoomIN does not run, or \"Allow KI-Debug\" is off: the user turns it on in "
        + "ZoomIN under General -> Diagnostics. The MCP server (ZoomIN.Mcp.exe) must run as the same Windows user.";

    public async Task<AgentResponse> SendAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.ConnectAsync(s_connectTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            log.NotReachable("timeout");
            return AgentResponse.Fail(NotReachableText);
        }
        catch (UnauthorizedAccessException)
        {
            // CurrentUserOnly: the pipe belongs to another user.
            log.NotReachable("other_user");
            return AgentResponse.Fail(NotReachableText);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(s_answerTimeout);
        try
        {
            await AgentFrames.WriteRequestAsync(pipe, request, timeout.Token).ConfigureAwait(false);
            return await AgentFrames.ReadResponseAsync(pipe, timeout.Token).ConfigureAwait(false)
                ?? AgentResponse.Fail("ZoomIN closed the connection without an answer.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            log.NotReachable("no_answer");
            return AgentResponse.Fail("ZoomIN did not answer within 30 s.");
        }
        catch (IOException)
        {
            log.NotReachable("pipe_broken");
            return AgentResponse.Fail("The connection to ZoomIN broke; KI-Debug may have been turned off.");
        }
    }
}
