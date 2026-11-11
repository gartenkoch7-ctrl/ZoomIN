namespace ZoomIN.Core.AgentDebug;

/// <summary>One parameter of an agent tool; becomes a property of the MCP input schema.</summary>
/// <param name="Type">JSON schema type: "string" or "integer".</param>
/// <param name="Choices">Allowed values of a string, or empty for free text.</param>
public sealed record AgentToolParameter(string Name, string Type, string Description, IReadOnlyList<string> Choices, bool Required = false);
