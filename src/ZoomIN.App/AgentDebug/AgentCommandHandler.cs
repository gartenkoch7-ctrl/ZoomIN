using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Zoom;
using ZoomIN.Core.AgentDebug;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.AgentDebug;

/// <summary>
/// Carries out the tool calls that arrive over the KI-Debug pipe (ADR-0027). Runs on the pipe's
/// thread, like hotkeys run on the hotkey thread; the commands are thread-safe the same way.
/// </summary>
public sealed class AgentCommandHandler(
    AgentStateReport state, AgentSnapshot snapshot, ZoomCommands zoom, WindowFilterCommands windowFilter,
    EffectsModel effects, TrackingModel tracking, ILogger<AgentCommandHandler> log)
{
    public async Task<AgentResponse> HandleAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tool = AgentTools.All.Any(t => t.Name == request.Tool) ? request.Tool : "unknown";
        using var activity = Telemetry.Source.StartActivity("zoomin.agent.command")?.SetTag("zoomin.agent.tool", tool);
        AgentResponse response;
        try
        {
            response = await RunAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Whatever goes wrong in one command must neither end the pipe nor ZoomIN.
            log.CommandFailed(ex, tool);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            return AgentResponse.Fail($"{tool} failed inside ZoomIN ({ex.GetType().Name}); see the ZoomIN log, event 1403.");
        }

        log.Command(tool, response.Ok ? "ok" : "failed");
        if (!response.Ok)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "failed");
        }

        return response;
    }

    private async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        switch (request.Tool)
        {
            case AgentTools.GetState:
                return AgentResponse.Done(state.Build());

            case AgentTools.TakeSnapshot:
                return AgentSnapshotOptions.TryParse(request, out var options, out var error)
                    ? await snapshot.TakeAsync(options, cancellationToken).ConfigureAwait(false)
                    : AgentResponse.Fail(error);

            case AgentTools.RunShortcut:
                var name = request.Argument(AgentTools.ShortcutParameter);
                return AgentShortcuts.TryParse(name, out var shortcut)
                    ? AgentResponse.Done(Run(shortcut))
                    : AgentResponse.Fail("shortcut must be one of: " + string.Join(", ", AgentShortcuts.Names));

            default:
                return AgentResponse.Fail($"Unknown tool '{request.Tool}'. Tools: " + string.Join(", ", AgentTools.All.Select(t => t.Name)));
        }
    }

    private string Run(AgentShortcut shortcut)
    {
        switch (shortcut)
        {
            case AgentShortcut.ToggleZoom: zoom.Toggle(); break;
            case AgentShortcut.ZoomIn: zoom.ZoomBy(1); break;
            case AgentShortcut.ZoomOut: zoom.ZoomBy(-1); break;
            case AgentShortcut.Fullscreen: zoom.Activate(ZoomMode.Fullscreen); break;
            case AgentShortcut.Lens: zoom.Activate(ZoomMode.Lens); break;
            case AgentShortcut.Docked: zoom.Activate(ZoomMode.Docked); break;
            case AgentShortcut.ToggleFilter: zoom.ToggleFilter(); break;
            case AgentShortcut.NextFilter: zoom.NextFilter(); break;
            case AgentShortcut.ToggleCrosshair: effects.ToggleCrosshair(); break;
            case AgentShortcut.ToggleTracking: tracking.ToggleEnabled(); break;
            case AgentShortcut.ToggleWindowFilter:
                return windowFilter.Toggle() is { } rule
                    ? $"Filter per window: {rule.Process} / {rule.WindowClass} -> {rule.Choice} {(rule.Choice == Core.WindowFilters.WindowFilterChoice.Filter ? rule.Filter : string.Empty)}".TrimEnd()
                    : "Nothing changed: the filter per window is off, or the foreground window is not one it acts on.";
        }

        return $"Done: {AgentShortcuts.NameOf(shortcut)} (monitor under the mouse: {zoom.MonitorUnderMouse()})";
    }
}
