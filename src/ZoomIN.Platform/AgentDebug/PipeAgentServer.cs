using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.AgentDebug;

namespace ZoomIN.Platform.AgentDebug;

/// <summary>
/// The running ZoomIN's end of the KI-Debug link (ADR-0027): a named pipe that only the same user
/// can open (<see cref="PipeOptions.CurrentUserOnly"/>, which also refuses remote clients). One
/// request per connection, and a client must send it at once: an idle client would otherwise hold
/// the only pipe instance and lock every other caller out.
/// </summary>
public sealed class PipeAgentServer(ILogger<PipeAgentServer> log, string pipeName) : IAgentDebugServer
{
    private static readonly TimeSpan s_requestTimeout = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
    private CancellationTokenSource? _stop;
    private Task? _loop;

    public PipeAgentServer(ILogger<PipeAgentServer> log)
        : this(log, SessionPipeName())
    {
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _stop is not null;
            }
        }
    }

    /// <summary>The pipe of this Windows session; the MCP role connects to the same name.</summary>
    public static string SessionPipeName()
    {
        using var process = Process.GetCurrentProcess();
        return AgentProtocol.PipeName(process.SessionId);
    }

    public void Start(Func<AgentRequest, CancellationToken, Task<AgentResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (_stop is not null)
            {
                return;
            }

            _stop = new CancellationTokenSource();
            var token = _stop.Token;
            _loop = Task.Run(() => ServeAsync(handler, token), CancellationToken.None);
        }

        log.PipeOpened();
    }

    /// <summary>Does not wait: it runs on the UI thread when the user turns the switch off.</summary>
    public void Stop() => Stop(wait: false);

    /// <summary>At exit: waits briefly, so the pipe is gone before the process ends its logging.</summary>
    public void Dispose() => Stop(wait: true);

    private void Stop(bool wait)
    {
        CancellationTokenSource? stop;
        Task? loop;
        lock (_gate)
        {
            stop = _stop;
            loop = _loop;
            _stop = null;
            _loop = null;
        }

        if (stop is null)
        {
            return;
        }

        stop.Cancel();
        if (wait)
        {
            try
            {
                // Bounded: the loop only awaits cancellable pipe calls and the handler.
                loop?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Cancelled or failed while stopping; it is gone either way.
            }
        }

        // The loop holds the token, not the source; disposing the source is safe while it ends.
        stop.Dispose();
        log.PipeClosed();
    }

    private async Task ServeAsync(Func<AgentRequest, CancellationToken, Task<AgentResponse>> handler, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);

                AgentRequest? request;
                using (var receive = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    receive.CancelAfter(s_requestTimeout);
                    request = await AgentFrames.ReadRequestAsync(pipe, receive.Token).ConfigureAwait(false);
                }

                if (request is null)
                {
                    continue;
                }

                var response = request.Version == AgentProtocol.Version
                    ? await handler(request, token).ConfigureAwait(false)
                    : AgentResponse.Fail($"Protocol version {request.Version} not supported; this ZoomIN speaks {AgentProtocol.Version}. Use the ZoomIN.Mcp.exe of the running version.");
                await AgentFrames.WriteResponseAsync(pipe, response, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException
                or UnauthorizedAccessException or OperationCanceledException)
            {
                // A client that idles, hangs up mid-frame or sends garbage must not end the link.
                log.ConnectionFailed(exception);
                await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
