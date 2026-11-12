using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using ZoomIN.Core.Folders;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Platform.Interop;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// The elevated step "ZoomIN.exe --copy-secure-logs &lt;folder&gt;" (M10, Q7): copies the log and
/// operations files of the secure instance from SYSTEM's profile into a new folder inside the
/// diagnostics folder of the user who asked, for the diagnostics package. It runs as administrator
/// and writes where that user can write, so it trusts nothing there (<see cref="SecureLogCopyRule"/>):
/// the diagnostics folder is checked for links once and then only used through its handle; the new
/// folder and every file in it are created relative to handles (<see cref="NtFiles"/>), never by
/// path. The new folder gets an access list the user can only read and delete.
/// No logger on purpose: a log file of this step would be written as administrator into the user's
/// own %LocalAppData%\ZoomIN\logs, which the user can redirect. The exit code says what happened.
/// </summary>
public sealed class SecureLogCopy
{
    public const int Copied = 0;
    public const int Failed = 1;
    public const int Refused = 2;

    // NTFS limit for one name.
    private const int MaxNameLength = 255;

    private readonly IAppFolders _folders;
    private readonly IAppFolders _system;
    private readonly Func<DirectorySecurity> _security;

    public SecureLogCopy(IAppFolders folders)
        : this(folders, SystemZoomFolder(), TargetSecurity)
    {
    }

    /// <param name="systemZoomFolder">SYSTEM's "…\AppData\Local\ZoomIN" (tests: a temporary folder).</param>
    /// <param name="security">Access list of the new folder (tests: one the non-elevated test may write to).</param>
    internal SecureLogCopy(IAppFolders folders, string systemZoomFolder, Func<DirectorySecurity> security)
    {
        _folders = folders;
        _system = new SystemFolders(systemZoomFolder);
        _security = security;
    }

    /// <returns>The exit code: <see cref="Copied"/>, <see cref="Refused"/> or <see cref="Failed"/>.</returns>
    public int Run(string target)
    {
        // String rules first: nothing may touch a UNC path or a path that hides where it leads.
        var diagnostics = _folders.Diagnostics();
        if (!SecureLogCopyRule.IsAllowedTarget(target, diagnostics))
        {
            return Refused;
        }

        try
        {
            if (new DriveInfo(diagnostics[..3]).DriveType != DriveType.Fixed)
            {
                return Refused;
            }

            using var parent = HeldDirectory.TryOpen(diagnostics);
            if (parent is null)
            {
                return Refused;
            }

            var status = NtFiles.CreateDirectory(parent.Handle, Path.GetFileName(target), _security().GetSecurityDescriptorBinaryForm(), out var folder);
            using (folder)
            {
                if (status == NtFiles.NameCollision)
                {
                    return Refused;
                }

                if (status < 0)
                {
                    return Failed;
                }

                return CopyFiles(folder);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // No log and no crash handler in this step on purpose (see above): the exit code is the report.
            return Failed;
        }
    }

    private int CopyFiles(SafeFileHandle folder)
    {
        var sources = Candidates(_system.Logs(), "logs-", recurse: false).Concat(Candidates(_system.Debug(), "debug-", recurse: true)).ToList();
        foreach (var (file, name) in SecureLogCopyRule.Select(sources, s => s.File.Length, s => s.File.LastWriteTimeUtc))
        {
            // The secure instance may prune or rotate a log meanwhile (the UAC prompt starts it): skip that one.
            using var source = TryOpen(file);
            if (source is null)
            {
                continue;
            }

            // A new name in the new folder: FILE_CREATE, relative to the folder's handle.
            var status = NtFiles.CreateFile(folder, name, out var created);
            if (status < 0)
            {
                created.Dispose();

                // Two debug files can flatten to the same name ("a_b\x" and "a\b_x"): keep the first.
                if (status == NtFiles.NameCollision)
                {
                    continue;
                }

                return Failed;
            }

            using var copy = new FileStream(created, FileAccess.Write);
            source.CopyTo(copy);
        }

        return Copied;
    }

    private static FileStream? TryOpen(FileInfo file)
    {
        try
        {
            return new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Log files below <paramref name="folder"/> (SYSTEM's profile, admin-only), named flat with their origin.</summary>
    private static List<(FileInfo File, string Name)> Candidates(string folder, string prefix, bool recurse)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        // The secure instance may remove a snapshot folder meanwhile: a folder that cannot be listed
        // costs its own files, not the whole copy. A flattened name longer than NTFS allows is left out.
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = recurse, IgnoreInaccessible = true };
            return [.. new DirectoryInfo(folder).EnumerateFiles("*", options)
                .Where(f => SecureLogCopyRule.IsCopiedFile(f.Name))
                .Select(f => (f, prefix + Path.GetRelativePath(folder, f.FullName).Replace('\\', '_')))
                .Where(c => c.Item2.Length <= MaxNameLength)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// SYSTEM and administrators may do anything; the user who asked may read and delete (the
    /// package removes the folder once zipped) but not add entries, write attributes or change the list.
    /// </summary>
    internal static DirectorySecurity TargetSecurity()
    {
        const InheritanceFlags Inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        using var identity = WindowsIdentity.GetCurrent();
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var security = new DirectorySecurity();

        // The owner may always change the access list; with the user as owner (a policy can make the
        // creator the owner) the user could grant themselves the right to add a link after all.
        security.SetOwner(administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, Inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            administrators, FileSystemRights.FullControl, Inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            identity.User!, FileSystemRights.ReadAndExecute | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles,
            Inherit, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }

    // SYSTEM's own profile, where the secure instance keeps its logs (ADR-0016); only SYSTEM and
    // administrators can write there.
    private static string SystemZoomFolder() =>
        Path.Combine(Environment.SystemDirectory, "config", "systemprofile", "AppData", "Local", "ZoomIN");

    /// <summary>The secure instance's folders as it sees them (its Local is SYSTEM's), for the AppFiles layout.</summary>
    private sealed record SystemFolders(string Local) : IAppFolders
    {
        // The secure instance keeps nothing roaming that is copied here.
        public string Roaming => Local;
    }
}
