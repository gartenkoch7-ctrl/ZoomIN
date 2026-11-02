using System.IO.Compression;
using System.Security.Cryptography;
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
    private readonly ILog _log;

    public UpdateApplier(ISignatureVerifier verifier, ILog log)
    {
        _verifier = verifier;
        _log = log;
    }

    public UpdateResult Apply(InstallLayout layout, UpdateCommand command)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(command);
        var previous = ActiveVersion(layout);
        if (previous == command.Version)
        {
            return UpdateResult.AlreadyActive;
        }

        var target = layout.VersionFolder(command.Version);
        var staging = target + InstallLayout.StagingSuffix;
        var package = staging + ".zip";
        var result = UpdateResult.Failed;
        try
        {
            DeleteFolder(staging);
            File.Copy(command.PackagePath, package, overwrite: true);
            if (!HashMatches(package, command.Sha256))
            {
                return result = UpdateResult.PackageMismatch;
            }

            ZipFile.ExtractToDirectory(package, staging);
            if (_verifier.Verify(Path.Combine(staging, InstallLayout.ExecutableName)) != PackageCheck.Ok)
            {
                return result = UpdateResult.SignatureRejected;
            }

            DeleteFolder(target);
            Directory.Move(staging, target);
            SwitchCurrent(layout, target);
            RemoveOldFolders(layout, command.Version, previous);
            _log.Info($"[Update] {previous?.ToString() ?? "-"} -> {command.Version} installed.");
            return result = UpdateResult.Installed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.Error($"[Update] Installing {command.Version} failed", ex);
            return UpdateResult.Failed;
        }
        finally
        {
            TryDelete(() => File.Delete(package));
            if (result != UpdateResult.Installed)
            {
                TryDelete(() => DeleteFolder(staging));
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

    private static bool HashMatches(string path, string expectedHex)
    {
        using var stream = File.OpenRead(path);
        var actual = SHA256.HashData(stream);
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expectedHex));
    }

    // A directory link cannot be replaced in one step; the new link is prepared next to it, so the
    // gap without "current" is a single delete + rename.
    private static void SwitchCurrent(InstallLayout layout, string target)
    {
        var next = layout.Current + ".new";
        DeleteFolder(next);
        Directory.CreateSymbolicLink(next, target);
        DeleteFolder(layout.Current);
        Directory.Move(next, layout.Current);
    }

    private void RemoveOldFolders(InstallLayout layout, AppVersion active, AppVersion? previous)
    {
        var names = Directory.EnumerateDirectories(layout.Root).Select(Path.GetFileName).OfType<string>();
        foreach (var name in InstallLayout.FoldersToRemove(names, active, previous))
        {
            // Still in use (an old ZoomIN that did not exit yet): the next update removes it.
            if (!TryDelete(() => Directory.Delete(Path.Combine(layout.Root, name), recursive: true)))
            {
                _log.Info($"[Update] {name} kept for now, still in use.");
            }
        }
    }

    // Removes a folder or a directory link; for a link only the link goes, never its target.
    private static void DeleteFolder(string path)
    {
        var info = new DirectoryInfo(path);
        if (info.LinkTarget is not null)
        {
            info.Delete();
        }
        else if (info.Exists)
        {
            info.Delete(recursive: true);
        }
    }

    private static bool TryDelete(Action delete)
    {
        try
        {
            delete();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
