using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// Finds the folders ZoomIN reads on a secure desktop (ADR-0016). Windows starts the secure
/// instance as SYSTEM inside the user's session; the user's settings then live in that user's
/// roaming AppData, not in SYSTEM's. Started as a normal user (developer switch), it is the own folder.
/// </summary>
public static unsafe class SessionUserFolders
{
    public static bool IsSystem
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.IsWellKnown(WellKnownSidType.LocalSystemSid) == true;
        }
    }

    /// <summary>
    /// Settings copy and logs of the secure instance. As SYSTEM this is SYSTEM's own profile, which
    /// only SYSTEM and administrators can write. Not %ProgramData%: users may create folders there
    /// and could plant a link that redirects SYSTEM's writes.
    /// </summary>
    public static string MachineFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN");

    /// <returns>%AppData%\ZoomIN of the user signed in to this session, or null (e.g. sign-in after boot).</returns>
    public static string? SessionUserZoomFolder()
    {
        if (!IsSystem)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoomIN");
        }

        if (!PInvoke.ProcessIdToSessionId((uint)Environment.ProcessId, out var session))
        {
            return null;
        }

        // Fails with ERROR_NO_TOKEN while nobody is signed in to the session.
        HANDLE token;
        if (!PInvoke.WTSQueryUserToken(session, &token))
        {
            return null;
        }

        using var owned = new SafeFileHandle(token, ownsHandle: true);
        PWSTR path;
        var folder = PInvoke.FOLDERID_RoamingAppData;
        if (PInvoke.SHGetKnownFolderPath(&folder, KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT, (HANDLE)owned.DangerousGetHandle(), &path).Failed)
        {
            return null;
        }

        try
        {
            return Path.Combine(path.ToString(), "ZoomIN");
        }
        finally
        {
            PInvoke.CoTaskMemFree(path);
        }
    }
}
