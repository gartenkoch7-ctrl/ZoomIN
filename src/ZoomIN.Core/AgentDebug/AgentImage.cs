namespace ZoomIN.Core.AgentDebug;

/// <summary>A PNG image in an answer, e.g. what ZoomIN drew on one monitor.</summary>
/// <param name="Name">Technical name such as "DISPLAY1-result".</param>
/// <param name="Png">The PNG file content.</param>
public sealed record AgentImage(string Name, byte[] Png);
