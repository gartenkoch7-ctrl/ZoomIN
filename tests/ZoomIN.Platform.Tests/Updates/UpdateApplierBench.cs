using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>A temp "Program Files\ZoomIN" with fakes for the install step's checks (UpdateApplier tests).</summary>
internal sealed class UpdateApplierBench : IDisposable
{
    public UpdateApplierBench()
    {
        Layout = new InstallLayout(Directory.CreateDirectory(Path.Combine(Temp, "ZoomIN")).FullName);
        Applier = new UpdateApplier(Verifier, Signer, Checksums, NullLogger.Instance);
    }

    public string Temp { get; } = Directory.CreateTempSubdirectory("ZoomIN.Tests.").FullName;

    public InstallLayout Layout { get; }

    public FakeVerifier Verifier { get; } = new();

    public FakeLocalSigner Signer { get; } = new();

    public FakeReleaseChecksums Checksums { get; } = new();

    public UpdateApplier Applier { get; }

    public void Dispose() => Directory.Delete(Temp, recursive: true);

    public UpdateCommand Package(string version)
    {
        Assert.True(AppVersion.TryParse(version, out var parsed));
        var source = Directory.CreateDirectory(Path.Combine(Temp, "src-" + version)).FullName;
        File.WriteAllText(Path.Combine(source, InstallLayout.ExecutableName), version);
        var zip = Path.Combine(Temp, $"package-{version}.zip");
        File.Delete(zip);
        ZipFile.CreateFromDirectory(source, zip);
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
        var command = new UpdateCommand(zip, hash, parsed);

        // Every package is published unless a test unpublishes it (the elevated step checks it, ADR-0031).
        Checksums.Publish(command);
        return command;
    }

    public string[] Folders() =>
        [.. Directory.GetDirectories(Layout.Root).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];

    public bool CanCreateLinks()
    {
        try
        {
            var probe = Path.Combine(Temp, "probe");
            Directory.CreateSymbolicLink(probe, Temp);
            Directory.Delete(probe);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal sealed class FakeVerifier : ISignatureVerifier
    {
        public PackageCheck Result { get; set; } = PackageCheck.Ok;

        public PackageCheck Verify(string executablePath) => File.Exists(executablePath) ? Result : PackageCheck.NotSigned;
    }
}
