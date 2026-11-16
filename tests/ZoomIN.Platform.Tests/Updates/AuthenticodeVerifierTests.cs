using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>
/// Uses the Microsoft-signed runtime library as a real signed file, so no ZoomIN certificate is
/// needed on the test machine. Copies live in a private temp folder.
/// </summary>
public sealed class AuthenticodeVerifierTests : IDisposable
{
    private static readonly string s_signedByMicrosoft = typeof(object).Assembly.Location;
    private readonly string _directory = Directory.CreateTempSubdirectory("ZoomIN.Tests.").FullName;
    private readonly AuthenticodeVerifier _verifier = new();

    [Fact]
    public void ValidSignatureOfOtherPublisher_IsRejected()
    {
        Assert.Equal(PackageCheck.UnexpectedPublisher, _verifier.Verify(s_signedByMicrosoft));
    }

    [Fact]
    public void TamperedFile_FailsTrust()
    {
        var copy = Path.Combine(_directory, "tampered.dll");
        var bytes = File.ReadAllBytes(s_signedByMicrosoft);
        bytes[bytes.Length / 2] ^= 0xFF;
        File.WriteAllBytes(copy, bytes);

        Assert.Equal(PackageCheck.SignatureInvalid, _verifier.Verify(copy));
    }

    [Theory]
    [InlineData("unsigned.exe", "MZ not really a program")]
    [InlineData("notes.txt", "plain text")]
    public void FileWithoutSignature_IsNotSigned(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);

        Assert.Equal(PackageCheck.NotSigned, _verifier.Verify(path));
    }

    [Fact]
    public void InstalledLocalBuild_IsTrusted()
    {
        // Only meaningful on a PC set up with scripts/setup-signing.ps1 + install-uiaccess.ps1;
        // elsewhere (CI) there is no ZoomIN signature to check.
        var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ZoomIN", "ZoomIN.exe");
        if (!File.Exists(installed))
        {
            return;
        }

        Assert.Equal(PackageCheck.Ok, _verifier.Verify(installed));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
