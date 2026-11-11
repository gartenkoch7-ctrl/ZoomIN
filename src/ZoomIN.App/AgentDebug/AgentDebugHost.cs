using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.AgentDebug;

namespace ZoomIN.App.AgentDebug;

/// <summary>
/// Opens the KI-Debug pipe while the user allows it (ADR-0027) and closes it as soon as the switch
/// goes off. Only the tray app has one; the secure-desktop instance never does (ADR-0016).
/// </summary>
public sealed class AgentDebugHost(
    SettingsService settings, IAgentDebugServer server, AgentCommandHandler handler, ILogger<AgentDebugHost> log) : IDisposable
{
    private readonly Lock _gate = new();
    private bool _started;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        settings.Changed += Apply;
        Apply();
    }

    public void Dispose()
    {
        if (_started)
        {
            settings.Changed -= Apply;
        }

        server.Stop();
    }

    // Settings change on any thread; the lock keeps "is it running" and start/stop together.
    private void Apply()
    {
        lock (_gate)
        {
            var allowed = settings.Current.AgentDebugEnabled;
            if (allowed == server.IsRunning)
            {
                return;
            }

            if (allowed)
            {
                server.Start(handler.HandleAsync);
            }
            else
            {
                server.Stop();
            }

            log.Allowed(allowed);
        }
    }
}
