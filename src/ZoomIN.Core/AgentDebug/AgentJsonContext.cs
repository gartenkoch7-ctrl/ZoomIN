using System.Text.Json.Serialization;

namespace ZoomIN.Core.AgentDebug;

/// <summary>JSON of the pipe frames, source generated (no reflection). Images are base64 in the JSON.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AgentRequest))]
[JsonSerializable(typeof(AgentResponse))]
public sealed partial class AgentJsonContext : JsonSerializerContext;
