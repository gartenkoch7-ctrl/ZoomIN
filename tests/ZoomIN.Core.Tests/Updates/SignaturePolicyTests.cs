using ZoomIN.Core.Updates;

namespace ZoomIN.Core.Tests.Updates;

public sealed class SignaturePolicyTests
{
    [Theory]
    [InlineData(PackageCheck.Ok)]
    [InlineData(PackageCheck.NotSigned)]
    public void UnsignedOrValidlySigned_IsAccepted(PackageCheck check) => Assert.True(SignaturePolicy.Accepts(check));

    [Theory]
    [InlineData(PackageCheck.SignatureInvalid)]
    [InlineData(PackageCheck.UnexpectedPublisher)]
    [InlineData(PackageCheck.ChecksumMismatch)]
    [InlineData(PackageCheck.ChecksumMissing)]
    [InlineData(PackageCheck.DownloadFailed)]
    public void BrokenOrForeign_IsRefused(PackageCheck check) => Assert.False(SignaturePolicy.Accepts(check));
}
