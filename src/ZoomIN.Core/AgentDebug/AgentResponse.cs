namespace ZoomIN.Core.AgentDebug;

/// <summary>The answer of the running ZoomIN to one <see cref="AgentRequest"/> (ADR-0027).</summary>
/// <param name="Ok">False if the tool failed or its arguments were wrong.</param>
/// <param name="Text">What happened, for the agent to read. No user content (AGENTS.md 2.5).</param>
/// <param name="Images">Images of the answer; empty for most tools.</param>
public sealed record AgentResponse(bool Ok, string Text, IReadOnlyList<AgentImage> Images)
{
    public static AgentResponse Done(string text) => new(true, text, []);

    public static AgentResponse Fail(string text) => new(false, text, []);
}
