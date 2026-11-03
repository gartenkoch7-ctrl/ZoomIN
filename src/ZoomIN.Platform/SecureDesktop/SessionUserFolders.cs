using System.Security.Principal;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>Where the secure-desktop instance keeps its own files (ADR-0016).</summary>
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

    /// <summary>
    /// Settings copy and logs of the secure instance. As SYSTEM this is SYSTEM's own profile, which
    /// only SYSTEM and administrators can write. Not %ProgramData%: users may create folders there
    /// and could plant a link that redirects SYSTEM's writes.
    /// </summary>
    public static string MachineFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN");
}
