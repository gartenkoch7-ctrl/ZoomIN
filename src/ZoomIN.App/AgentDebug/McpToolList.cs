using System.Text.Json;
using ModelContextProtocol.Protocol;
using ZoomIN.Core.AgentDebug;

namespace ZoomIN.App.AgentDebug;

/// <summary>Turns <see cref="AgentTools"/> into MCP tools with JSON input schemas (ADR-0027).</summary>
internal static class McpToolList
{
    public static IList<Tool> Build() => [.. AgentTools.All.Select(ToTool)];

    /// <summary>MCP arguments as the text the pipe carries: strings as they are, numbers and the rest as raw JSON.</summary>
    public static Dictionary<string, string> Arguments(IDictionary<string, JsonElement>? arguments)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments ?? new Dictionary<string, JsonElement>())
        {
            result[name] = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
        }

        return result;
    }

    private static Tool ToTool(AgentTool tool) => new()
    {
        Name = tool.Name,
        Description = tool.Description,
        InputSchema = Schema(tool.Parameters),
    };

    private static JsonElement Schema(IReadOnlyList<AgentToolParameter> parameters)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("type", "object");
            json.WriteStartObject("properties");
            foreach (var p in parameters)
            {
                json.WriteStartObject(p.Name);
                json.WriteString("type", p.Type);
                json.WriteString("description", p.Description);
                if (p.Choices.Count > 0)
                {
                    json.WriteStartArray("enum");
                    foreach (var choice in p.Choices)
                    {
                        json.WriteStringValue(choice);
                    }

                    json.WriteEndArray();
                }

                json.WriteEndObject();
            }

            json.WriteEndObject();
            json.WriteStartArray("required");
            foreach (var p in parameters.Where(p => p.Required))
            {
                json.WriteStringValue(p.Name);
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.ToArray());
        return document.RootElement.Clone();
    }
}
