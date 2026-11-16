using System.Text.Json;
using ZoomIN.App.AgentDebug;
using ZoomIN.Core.AgentDebug;

namespace ZoomIN.App.Tests.AgentDebug;

public sealed class McpToolListTests
{
    [Fact]
    public void EveryAgentTool_BecomesAnMcpTool_WithAnObjectSchema()
    {
        var tools = McpToolList.Build();

        Assert.Equal(AgentTools.All.Select(t => t.Name), tools.Select(t => t.Name));
        Assert.All(tools, t => Assert.Equal("object", t.InputSchema.GetProperty("type").GetString()));
    }

    [Fact]
    public void Shortcut_IsRequired_AndListsItsChoices()
    {
        var schema = McpToolList.Build().Single(t => t.Name == AgentTools.RunShortcut).InputSchema;

        Assert.Equal("shortcut", schema.GetProperty("required")[0].GetString());
        var choices = schema.GetProperty("properties").GetProperty("shortcut").GetProperty("enum").EnumerateArray().Select(e => e.GetString());
        Assert.Equal(AgentShortcuts.Names, choices);
    }

    [Fact]
    public void Arguments_KeepStrings_AndTurnNumbersIntoText()
    {
        using var document = JsonDocument.Parse("""{"crop":"1,2,3,4","max_width":800}""");
        var input = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

        var arguments = McpToolList.Arguments(input);

        Assert.Equal("1,2,3,4", arguments["crop"]);
        Assert.Equal("800", arguments["max_width"]);
        Assert.Empty(McpToolList.Arguments(null));
    }

    [Theory]
    [InlineData(new[] { "--mcp" }, @"C:\Program Files\ZoomIN\current\ZoomIN.exe", true)]
    [InlineData(new string[0], @"C:\Program Files\ZoomIN\current\ZoomIN.Mcp.exe", true)]
    [InlineData(new[] { "--traceparent-free" }, @"D:\bin\zoomin.mcp.EXE", true)]
    [InlineData(new string[0], @"C:\Program Files\ZoomIN\current\ZoomIN.exe", false)]
    [InlineData(new[] { "--mcp", "x" }, @"C:\ZoomIN.exe", false)]
    public void McpRole_RunsForTheSwitchOrTheMcpHost(string[] args, string path, bool expected)
    {
        Assert.Equal(expected, McpServerRole.IsRequested(args, path));
    }
}
