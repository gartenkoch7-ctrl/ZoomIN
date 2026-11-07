using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Folders;
using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// The user signed in to this session, seen from the secure-desktop instance (ADR-0016). Windows
/// starts that instance as SYSTEM inside the user's session; the user's settings live in that
/// user's roaming AppData. Every access to it runs impersonated as the user, so the user's own
/// file permissions apply and a redirected folder never gets SYSTEM's (the computer account's)
/// network sign-in. Started as a normal user (developer switch), it is simply the own folder.
/// </summary>
public sealed unsafe class SessionUser : IDisposable
{
    private readonly SafeAccessTokenHandle? _token;

    private SessionUser(string? zoomFolder, SafeAccessTokenHandle? token)
    {
        ZoomFolder = zoomFolder;
        _token = token;
    }

    /// <summary>%AppData%\ZoomIN of the session user, or null (nobody signed in, or not a local disk).</summary>
    public string? ZoomFolder { get; }

    /// <summary>No signed-in user: only the kept copy or defaults are used.</summary>
    public static SessionUser None { get; } = new(null, null);

    /// <summary>A plain folder, read as the current account (tests, developer switch).</summary>
    public static SessionUser ForFolder(string? zoomFolder) => new(zoomFolder, null);

    /// <param name="log">Only error codes are logged, never paths or names (secure log, ADR-0016).</param>
    /// <param name="folders">Own folders; used as they are when not running as SYSTEM.</param>
    public static SessionUser Open(ILogger log, IAppFolders folders)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(folders);
        if (!SessionUserFolders.IsSystem)
        {
            return ForFolder(folders.Roaming);
        }

        if (!PInvoke.ProcessIdToSessionId((uint)Environment.ProcessId, out var session))
        {
            log.SessionUnknown(Marshal.GetLastPInvokeError());
            return None;
        }

        if (SessionUserToken.Open(session, log) is not { } token)
        {
            return None;
        }

        var folder = WindowsIdentity.RunImpersonated(token, () => RoamingAppData(token, log));
        if (folder is null || !IsLocalFixedDisk(folder))
        {
            if (folder is not null)
            {
                log.AppDataNotLocal();
            }

            token.Dispose();
            return None;
        }

        return new SessionUser(Path.Combine(folder, "ZoomIN"), token);
    }

    /// <summary>Runs <paramref name="read"/> as the session user (or as is, without a token).</summary>
    public T Read<T>(Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return _token is null ? read() : WindowsIdentity.RunImpersonated(_token, read);
    }

    public void Dispose() => _token?.Dispose();

    /// <summary>Only "X:\…" on a fixed local disk: no UNC, WebDAV, mapped or SUBST network drive.</summary>
    internal static bool IsLocalFixedDisk(string path)
    {
        if (!LocalPathRule.IsDriveLetterPath(path))
        {
            return false;
        }

        try
        {
            return new DriveInfo(path[..3]).DriveType == DriveType.Fixed;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string? RoamingAppData(SafeAccessTokenHandle token, ILogger log)
    {
        PWSTR path;
        var folder = PInvoke.FOLDERID_RoamingAppData;
        var result = PInvoke.SHGetKnownFolderPath(&folder, KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT, (HANDLE)token.DangerousGetHandle(), &path);
        if (result.Failed)
        {
            log.AppDataUnknown(result.Value);
            return null;
        }

        try
        {
            return path.ToString();
        }
        finally
        {
            PInvoke.CoTaskMemFree(path);
        }
    }
}
