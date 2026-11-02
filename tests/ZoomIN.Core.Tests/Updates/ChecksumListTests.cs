using ZoomIN.Core.Updates;

namespace ZoomIN.Core.Tests.Updates;

public sealed class ChecksumListTests
{
    private const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
    private const string Package = "ZoomIN-1.0.0-win-x64-uiaccess.zip";

    [Theory]
    [InlineData(Hash + "  " + Package)]
    [InlineData(Hash + " *" + Package)]
    [InlineData("9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08  " + Package)]
    [InlineData("other  file.txt\r\n" + Hash + "  " + Package + "\r\n")]
    public void ListedPackage_IsFound(string content)
    {
        Assert.True(ChecksumList.TryFind(content, Package, out var sha256));
        Assert.Equal(Convert.FromHexString(Hash), sha256);
    }

    [Theory]
    [InlineData("")]
    [InlineData(Hash + "  ZoomIN-1.0.1-win-x64-uiaccess.zip")]
    [InlineData("9f86d081  " + Package)]
    [InlineData("zz86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08  " + Package)]
    [InlineData(Hash)]
    public void MissingOrBrokenEntry_IsNotFound(string content)
    {
        Assert.False(ChecksumList.TryFind(content, Package, out _));
    }

    [Theory]
    [InlineData("CN=ZoomIN Local Code Signing", true)]
    [InlineData("cn=zoomin local code signing", true)]
    [InlineData("CN=Microsoft Corporation, O=Microsoft Corporation", false)]
    [InlineData(null, false)]
    public void OnlyPinnedPublishers_AreTrusted(string? subject, bool expected)
    {
        Assert.Equal(expected, TrustedPublishers.IsTrusted(subject));
    }
}
