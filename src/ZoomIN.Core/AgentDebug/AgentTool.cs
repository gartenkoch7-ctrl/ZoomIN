namespace ZoomIN.Core.AgentDebug;

/// <summary>One tool of the KI-Debug MCP server (ADR-0027).</summary>
public sealed record AgentTool(string Name, string Description, IReadOnlyList<AgentToolParameter> Parameters);
