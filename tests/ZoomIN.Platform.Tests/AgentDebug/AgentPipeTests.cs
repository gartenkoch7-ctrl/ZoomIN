using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.AgentDebug;
using ZoomIN.Platform.AgentDebug;

namespace ZoomIN.Platform.Tests.AgentDebug;

public sealed class AgentPipeTests
{
    // Own pipe per test: never the real one of a running ZoomIN.
    private readonly string _pipe = "ZoomIN.AgentDebug.Test." + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Frames_RoundTrip_AndEndOfStreamIsNull()
    {
        using var stream = new MemoryStream();
        var request = new AgentRequest(AgentProtocol.Version, "get_state", new Dictionary<string, string>());

        await AgentFrames.WriteRequestAsync(stream, request, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var back = await AgentFrames.ReadRequestAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal("get_state", back!.Tool);
        Assert.Null(await AgentFrames.ReadRequestAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Frame_WithAnImpossibleLength_IsRefused()
    {
        using var stream = new MemoryStream([0xFF, 0xFF, 0xFF, 0x7F]);

        await Assert.ThrowsAsync<InvalidDataException>(() => AgentFrames.ReadRequestAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ClientAndServer_ExchangeRequests_OverTheNamedPipe()
    {
        using var server = new PipeAgentServer(NullLogger<PipeAgentServer>.Instance, _pipe);
        server.Start((request, _) => Task.FromResult(AgentResponse.Done("echo " + request.Tool)));
        var client = new PipeAgentClient(NullLogger<PipeAgentClient>.Instance, _pipe);

        var first = await client.SendAsync(Request("one"), TestContext.Current.CancellationToken);
        var second = await client.SendAsync(Request("two"), TestContext.Current.CancellationToken);

        Assert.True(first.Ok);
        Assert.Equal("echo one", first.Text);
        Assert.Equal("echo two", second.Text);
    }

    [Fact]
    public async Task WithoutServer_TheClientExplainsHowToTurnItOn()
    {
        var client = new PipeAgentClient(NullLogger<PipeAgentClient>.Instance, _pipe);

        var response = await client.SendAsync(Request("get_state"), TestContext.Current.CancellationToken);

        Assert.False(response.Ok);
        Assert.Equal(PipeAgentClient.NotReachableText, response.Text);
    }

    [Fact]
    public async Task AfterStop_ThePipeIsGone()
    {
        using var server = new PipeAgentServer(NullLogger<PipeAgentServer>.Instance, _pipe);
        server.Start((_, _) => Task.FromResult(AgentResponse.Done("ok")));
        var client = new PipeAgentClient(NullLogger<PipeAgentClient>.Instance, _pipe);
        Assert.True((await client.SendAsync(Request("x"), TestContext.Current.CancellationToken)).Ok);

        server.Stop();

        Assert.False(server.IsRunning);
        Assert.False((await client.SendAsync(Request("x"), TestContext.Current.CancellationToken)).Ok);
    }

    [Fact]
    public async Task OtherProtocolVersion_IsAnsweredWithAnError_NotPassedOn()
    {
        var handled = false;
        using var server = new PipeAgentServer(NullLogger<PipeAgentServer>.Instance, _pipe);
        server.Start((_, _) =>
        {
            handled = true;
            return Task.FromResult(AgentResponse.Done("ok"));
        });
        var client = new PipeAgentClient(NullLogger<PipeAgentClient>.Instance, _pipe);

        var response = await client.SendAsync(new AgentRequest(AgentProtocol.Version + 1, "get_state", new Dictionary<string, string>()), TestContext.Current.CancellationToken);

        Assert.False(response.Ok);
        Assert.False(handled);
    }

    private static AgentRequest Request(string tool) => new(AgentProtocol.Version, tool, new Dictionary<string, string>());
}
