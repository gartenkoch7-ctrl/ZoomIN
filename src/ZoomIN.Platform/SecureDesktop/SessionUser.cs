using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
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

    public static SessionUser Open()
    {
        if (!SessionUserFolders.IsSystem)
        {
            return ForFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoomIN"));
        }

        if (!PInvoke.ProcessIdToSessionId((uint)Environment.ProcessId, out var session))
        {
            return None;
        }

        // Fails with ERROR_NO_TOKEN while nobody is signed in to the session (sign-in after boot).
        HANDLE raw;
        if (!PInvoke.WTSQueryUserToken(session, &raw))
        {
            return None;
        }

        var token = new SafeAccessTokenHandle((nint)raw.Value);
        var folder = WindowsIdentity.RunImpersonated(token, () => RoamingAppData(token));
        if (folder is null || !IsLocalFixedDisk(folder))
        {
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

    private static string? RoamingAppData(SafeAccessTokenHandle token)
    {
        PWSTR path;
        var folder = PInvoke.FOLDERID_RoamingAppData;
        if (PInvoke.SHGetKnownFolderPath(&folder, KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT, (HANDLE)token.DangerousGetHandle(), &path).Failed)
        {
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
