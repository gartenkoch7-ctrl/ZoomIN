namespace ZoomIN.Core.AgentDebug;

/// <summary>One tool call from the MCP role to the running ZoomIN (ADR-0027).</summary>
/// <param name="Version">Protocol version of the sender; see <see cref="AgentProtocol.Version"/>.</param>
/// <param name="Tool">Tool name from <see cref="AgentTools"/>.</param>
/// <param name="Arguments">Tool arguments as text, e.g. "shortcut" → "lens".</param>
public sealed record AgentRequest(int Version, string Tool, IReadOnlyDictionary<string, string> Arguments)
{
    /// <summary>The argument, or null if it is missing or empty.</summary>
    public string? Argument(string name) =>
        Arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
