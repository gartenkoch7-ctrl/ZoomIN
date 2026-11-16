using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Core.Tests.Lifecycle;

/// <summary>Quoting as CommandLineToArgvW reads it back (ZoomIN starting itself through CreateProcess).</summary>
public sealed class CommandLineTests
{
    [Theory]
    [InlineData(new[] { "--left-job" }, "--left-job")]
    [InlineData(new[] { "--wait-for", "12" }, "--wait-for 12")]
    [InlineData(new[] { "" }, "\"\"")]
    [InlineData(new[] { @"C:\Program Files\ZoomIN\current\ZoomIN.exe" }, @"""C:\Program Files\ZoomIN\current\ZoomIN.exe""")]
    [InlineData(new[] { @"C:\dir\" }, @"C:\dir\")]
    [InlineData(new[] { @"C:\my dir\" }, @"""C:\my dir\\""")]
    [InlineData(new[] { "say \"hi\"" }, @"""say \""hi\""""")]
    [InlineData(new[] { @"a\""b" }, @"""a\\\""b""")]
    public void Join_QuotesOnlyWhatNeedsIt(string[] args, string expected) =>
        Assert.Equal(expected, CommandLine.Join(args));

    [Fact]
    public void Join_Nothing_IsEmpty() => Assert.Equal(string.Empty, CommandLine.Join([]));
}
