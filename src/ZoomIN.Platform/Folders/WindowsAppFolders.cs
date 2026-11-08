using ZoomIN.Core.Folders;

namespace ZoomIN.Platform.Folders;

/// <summary>
/// The ZoomIN folders of the running account, from Windows' known folders. Read once: the
/// account of a process does not change. As SYSTEM (secure desktop, ADR-0016) both lie in SYSTEM's
/// own profile, which only SYSTEM and administrators can write; never %ProgramData%, where users
/// could plant a link that redirects SYSTEM's writes.
/// </summary>
public sealed class WindowsAppFolders : IAppFolders
{
    public string Local { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN");

    public string Roaming { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoomIN");
}
