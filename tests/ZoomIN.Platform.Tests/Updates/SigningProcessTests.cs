using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>ADR-0031: values reach the elevated PowerShell only as safe single-quoted literals.</summary>
public sealed class SigningProcessTests
{
    [Theory]
    [InlineData("it's", "it''s")]
    [InlineData("a\u2018b\u2019c", "a\u2018\u2018b\u2019\u2019c")]
    [InlineData("a\u201Ab\u201Bc", "a\u201A\u201Ab\u201B\u201Bc")]
    [InlineData(@"C:\Program Files\ZoomIN\app-1.0.0.staging\ZoomIN.exe", @"C:\Program Files\ZoomIN\app-1.0.0.staging\ZoomIN.exe")]
    public void PowerShellLiteral_DoublesEveryQuote(string value, string quoted)
    {
        Assert.Equal(quoted, SigningProcess.Quote(value));
    }
}
