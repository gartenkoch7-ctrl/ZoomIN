using ZoomIN.Core.Updates;

namespace ZoomIN.Core.Tests.Updates;

public sealed class UpdateCommandTests
{
    private const string Hash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";

    [Fact]
    public void ValidArguments_AreParsed()
    {
        string[] args = [UpdateCommand.Switch, @"C:\Users\x\AppData\Local\ZoomIN\updates\p.zip", Hash, "1.1.0-dev.42"];

        Assert.True(UpdateCommand.TryParse(args, out var command));
        Assert.Equal(@"C:\Users\x\AppData\Local\ZoomIN\updates\p.zip", command.PackagePath);
        Assert.Equal(Hash.ToLowerInvariant(), command.Sha256);
        Assert.Equal(new AppVersion(1, 1, 0, "dev.42"), command.Version);
    }

    [Theory]
    [InlineData("--watchdog", "p.zip", Hash, "1.0.0")]
    [InlineData(UpdateCommand.Switch, "p.zip", "abc", "1.0.0")]
    [InlineData(UpdateCommand.Switch, "p.zip", "zz86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08", "1.0.0")]
    [InlineData(UpdateCommand.Switch, "p.zip", Hash, "latest")]
    public void InvalidArguments_AreRejected(string a, string b, string c, string d)
    {
        Assert.False(UpdateCommand.TryParse([a, b, c, d], out _));
    }

    [Fact]
    public void MissingArguments_AreRejected()
    {
        Assert.False(UpdateCommand.TryParse([UpdateCommand.Switch], out _));
    }

    [Fact]
    public void Format_QuotesThePath()
    {
        var command = new UpdateCommand(@"C:\My Files\p.zip", Hash.ToLowerInvariant(), new AppVersion(1, 0, 0));

        Assert.Equal($"--apply-update \"C:\\My Files\\p.zip\" {Hash.ToLowerInvariant()} 1.0.0", command.Format());
    }
}
