using System.Text.Json;
using ZoomIN.Core.AgentDebug;

namespace ZoomIN.Core.Tests.AgentDebug;

public sealed class AgentToolsTests
{
    [Fact]
    public void ToolAndParameterNames_AreUniqueSnakeCase()
    {
        var names = AgentTools.All.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        foreach (var tool in AgentTools.All)
        {
            Assert.Matches("^[a-z_]+$", tool.Name);
            Assert.All(tool.Parameters, p => Assert.Matches("^[a-z_]+$", p.Name));
            Assert.All(tool.Parameters, p => Assert.True(p.Type is "string" or "integer"));
        }
    }

    [Fact]
    public void EveryShortcut_HasAName_ThatParsesBack()
    {
        foreach (var shortcut in Enum.GetValues<AgentShortcut>())
        {
            var name = AgentShortcuts.NameOf(shortcut);
            Assert.True(AgentShortcuts.TryParse(name, out var parsed));
            Assert.Equal(shortcut, parsed);
        }

        Assert.Equal(Enum.GetValues<AgentShortcut>().Length, AgentShortcuts.Names.Count);
        Assert.False(AgentShortcuts.TryParse("format_disk", out _));
        Assert.False(AgentShortcuts.TryParse(null, out _));
    }

    [Fact]
    public void RequestAndResponse_SurviveTheJsonOfThePipe()
    {
        var request = new AgentRequest(AgentProtocol.Version, AgentTools.RunShortcut, new Dictionary<string, string> { ["shortcut"] = "lens" });
        var response = new AgentResponse(true, "Done", [new AgentImage("DISPLAY1-result", [1, 2, 3])]);

        var requestBack = JsonSerializer.Deserialize(JsonSerializer.Serialize(request, AgentJsonContext.Default.AgentRequest), AgentJsonContext.Default.AgentRequest)!;
        var responseBack = JsonSerializer.Deserialize(JsonSerializer.Serialize(response, AgentJsonContext.Default.AgentResponse), AgentJsonContext.Default.AgentResponse)!;

        Assert.Equal("lens", requestBack.Argument("shortcut"));
        Assert.Equal(AgentTools.RunShortcut, requestBack.Tool);
        Assert.True(responseBack.Ok);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(responseBack.Images).Png);
    }

    [Fact]
    public void PipeName_DependsOnTheSession()
    {
        Assert.NotEqual(AgentProtocol.PipeName(1), AgentProtocol.PipeName(2));
    }
}
