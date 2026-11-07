using System.Security.Principal;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// Whether this process runs as SYSTEM: then it is the secure-desktop instance (ADR-0016). Its own
/// files lie in SYSTEM's profile through <see cref="Core.Folders.IAppFolders"/>.
/// </summary>
public static class SessionUserFolders
{
    public static bool IsSystem
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.IsWellKnown(WellKnownSidType.LocalSystemSid) == true;
        }
    }
}
