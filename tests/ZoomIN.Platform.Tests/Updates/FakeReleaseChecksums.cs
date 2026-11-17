using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>Stands in for checksums.txt on GitHub: only versions published here have a checksum.</summary>
internal sealed class FakeReleaseChecksums : IReleaseChecksums
{
    private readonly Dictionary<AppVersion, byte[]> _published = [];

    /// <summary>Publishes the hash of <paramref name="command"/>'s package as its release checksum.</summary>
    public void Publish(UpdateCommand command) => _published[command.Version] = Convert.FromHexString(command.Sha256);

    /// <summary>The release of <paramref name="version"/> does not exist (or has no checksum for the package).</summary>
    public void Unpublish(AppVersion version) => _published.Remove(version);

    public byte[]? Published(AppVersion version) => _published.GetValueOrDefault(version);
}
