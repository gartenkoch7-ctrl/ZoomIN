using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// The elevated install step (ADR-0015): copies the package into Program Files, checks it there
/// again, unpacks it into its own version folder and points "current" at it. Checks run on the copy
/// in the admin-only folder, so nothing can swap the files between check and use.
/// </summary>
public sealed class UpdateApplier
{
    private readonly ISignatureVerifier _verifier;
    private readonly ILocalSigner _signer;
    private readonly IReleaseChecksums _checksums;
    private readonly ILogger _log;
    private readonly InstallFolders _folders;

    /// <param name="signer">Signs an unsigned release on this PC (ADR-0031).</param>
    /// <param name="checksums">The published checksum every package must match (fetched here, ADR-0031).</param>
    public UpdateApplier(ISignatureVerifier verifier, ILocalSigner signer, IReleaseChecksums checksums, ILogger log)
    {
        _verifier = verifier;
        _signer = signer;
        _checksums = checksums;
        _log = log;
        _folders = new InstallFolders(log);
    }

    public UpdateResult Apply(InstallLayout layout, UpdateCommand command)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(command);
        using var operation = UpdateTelemetry.Start("zoomin.update.apply", command.Version);
        var result = ApplyPackage(layout, command);
        return operation.Finish(result, UpdateTelemetry.Succeeded(result));
    }

    private UpdateResult ApplyPackage(InstallLayout layout, UpdateCommand command)
    {
        var previous = ActiveVersion(layout);

        // Only if Windows still trusts it: after an uninstall that left the folder behind, or a
        // certificate removed by hand, the same version is installed (and signed) again.
        if (previous == command.Version && _verifier.Verify(layout.CurrentExecutable) == PackageCheck.Ok)
        {
            return UpdateResult.AlreadyActive;
        }

        var target = layout.VersionFolder(command.Version);
        var staging = target + InstallLayout.StagingSuffix;
        var package = staging + ".zip";
        var result = UpdateResult.Failed;
        var signed = false;
        try
        {
            // Missing on a first install from the setup program.
            Directory.CreateDirectory(layout.Root);
            InstallFolders.DeleteFolder(staging);
            File.Copy(command.PackagePath, package, overwrite: true);
            if (!HashMatches(package, command.Sha256))
            {
                return result = UpdateResult.PackageMismatch;
            }

            // The hash on the command line comes from a non-elevated caller and proves nothing
            // (ADR-0031). Every package must match the checksum this step fetches itself from the
            // fixed release address – also one whose ZoomIN.exe is already signed: an installed,
            // locally signed exe next to someone else's DLLs would otherwise pass (review 2026-11-17).
            // No checksum (offline, no such release): the step cannot check, which is no tampering.
            if (_checksums.Published(command.Version) is not { } published)
            {
                return result = UpdateResult.Failed;
            }

            if (!HashMatches(package, published))
            {
                _log.PackageRefused(ReleaseAssets.PackageName(command.Version), "not the published release");
                return result = UpdateResult.PackageMismatch;
            }

            ZipFile.ExtractToDirectory(package, staging);

            // Releases are unsigned and get signed here; a broken or foreign signature is refused.
            var executable = Path.Combine(staging, InstallLayout.ExecutableName);
            var check = Verify(executable);
            if (!SignaturePolicy.Accepts(check))
            {
                return result = UpdateResult.SignatureRejected;
            }

            if (check == PackageCheck.NotSigned)
            {
                if (!_signer.Sign(executable))
                {
                    return result = UpdateResult.Failed;
                }

                signed = true;
            }

            _folders.MoveIntoPlace(layout, staging, target);

            // The same version again (re-signed): the way back to the real previous version stays.
            if (previous != command.Version)
            {
                _folders.RemoveOld(layout, command.Version, previous);
            }

            PruneCertificates(layout);
            _log.Installed(previous, command.Version);
            return result = UpdateResult.Installed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.InstallFailed(ex, command.Version);
            return UpdateResult.Failed;
        }
        finally
        {
            InstallFolders.TryDelete(() => File.Delete(package));
            if (result != UpdateResult.Installed)
            {
                InstallFolders.TryDelete(() => InstallFolders.DeleteFolder(staging));

                // Signed, then the move or switch failed: the new certificate signs nothing that
                // stays, so it must not stay trusted either.
                if (signed)
                {
                    PruneCertificates(layout);
                }
            }
        }
    }

    /// <summary>The version "current" points at, or null if there is no link yet.</summary>
    public static AppVersion? ActiveVersion(InstallLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var target = new DirectoryInfo(layout.Current).LinkTarget;
        return target is not null && InstallLayout.TryParseFolder(Path.GetFileName(Path.TrimEndingDirectorySeparator(target)), out var version)
            ? version
            : null;
    }

    private PackageCheck Verify(string executable)
    {
        using var operation = Telemetry.Source.StartActivity("zoomin.update.verify");
        var check = _verifier.Verify(executable);
        return operation.Finish(check, SignaturePolicy.Accepts(check));
    }

    private static bool HashMatches(string path, string expectedHex) => HashMatches(path, Convert.FromHexString(expectedHex));

    private static bool HashMatches(string path, byte[] expected)
    {
        using var stream = File.OpenRead(path);
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(stream), expected);
    }

    // Trust only the certificates of the version folders that stay (ADR-0031); after the switch,
    // so a failed install never costs the active version its trusted signature.
    // Best effort: the install itself already succeeded.
    private void PruneCertificates(InstallLayout layout)
    {
        try
        {
            var kept = Directory.EnumerateDirectories(layout.Root)
                .Where(folder => InstallLayout.TryParseFolder(Path.GetFileName(folder), out _))
                .Select(folder => Path.Combine(folder, InstallLayout.ExecutableName))
                .Where(File.Exists)
                .ToList();
            _signer.Prune(kept);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.CertificateCleanupSkipped("version folders unreadable");
        }
    }
}
