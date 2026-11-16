using System.Reflection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZoomIN.Core.AgentDebug;

namespace ZoomIN.App.AgentDebug;

/// <summary>
/// "ZoomIN.Mcp.exe" (ADR-0027): an MCP server on stdin/stdout for an agent such as Claude Code.
/// It holds no state of its own and passes every tool call over the pipe to the running ZoomIN.
/// Nothing may write to stdout except the MCP transport; logs go to the role's log file.
/// </summary>
public sealed class McpServerRole(IAgentDebugClient client, ILoggerFactory loggers)
{
    public const string Switch = "--mcp";

    /// <summary>
    /// Copy of the app host without the uiAccess manifest: CreateProcess (how MCP clients start a
    /// server) refuses UIAccess exes, so the installed ZoomIN.exe cannot be the server (ADR-0012).
    /// </summary>
    public const string HostFileName = "ZoomIN.Mcp.exe";

    /// <summary>True for "--mcp" and whenever the process was started as <see cref="HostFileName"/>.</summary>
    public static bool IsRequested(string[] args, string? processPath) =>
        args is [Switch] || string.Equals(Path.GetFileName(processPath), HostFileName, StringComparison.OrdinalIgnoreCase);

    private const string Instructions =
        "ZoomIN is a screen magnifier whose overlay is hidden from screen captures, so screenshots never show what it "
        + "draws. Use take_snapshot to see it, get_state for the numbers, run_shortcut to change zoom and filters. "
        + "The user must turn on \"Allow KI-Debug\" in ZoomIN (General -> Diagnostics) first.";

    public int Run()
    {
        var log = loggers.CreateLogger<McpServerRole>();
        log.McpStarted();
        try
        {
            RunAsync().GetAwaiter().GetResult();
            return 0;
        }
        finally
        {
            log.McpEnded();
        }
    }

    private async Task RunAsync()
    {
        var version = typeof(McpServerRole).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";
        var tools = McpToolList.Build();
        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "zoomin", Title = "ZoomIN KI-Debug", Version = version },
            ServerInstructions = Instructions,
            Capabilities = new ServerCapabilities { Tools = new ToolsCapability() },
            Handlers = new McpServerHandlers
            {
                ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = tools }),
                CallToolHandler = CallAsync,
            },
        };

        await using var transport = new StdioServerTransport("zoomin", loggers);
        await using var server = McpServer.Create(transport, options, loggers);
        await server.RunAsync().ConfigureAwait(false);
    }

    private async ValueTask<CallToolResult> CallAsync(RequestContext<CallToolRequestParams> context, CancellationToken cancellationToken)
    {
        var call = context.Params ?? throw new InvalidOperationException("tools/call without parameters");
        var request = new AgentRequest(AgentProtocol.Version, call.Name, McpToolList.Arguments(call.Arguments));
        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var content = new List<ContentBlock>();
        if (response.Text.Length > 0 || response.Images.Count == 0)
        {
            content.Add(new TextContentBlock { Text = response.Text.Length > 0 ? response.Text : "Done." });
        }

        foreach (var image in response.Images)
        {
            content.Add(new TextContentBlock { Text = image.Name });
            content.Add(ImageContentBlock.FromBytes(image.Png, "image/png"));
        }

        return new CallToolResult { Content = content, IsError = !response.Ok };
    }
}
