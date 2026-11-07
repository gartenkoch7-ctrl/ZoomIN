using System.Globalization;
using ZoomIN.Core.Folders;
using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Watchdog.Crashes;

/// <summary>
/// Remembers that the watchdog gave up on the secure-desktop instance (ADR-0016). Stored next to
/// the crash log, which for SYSTEM is in SYSTEM's own profile.
/// </summary>
public sealed class SecureDesktopGiveUp(IAppFolders folders, TimeProvider time)
{
    public SecureDesktopGiveUp(IAppFolders folders)
        : this(folders, TimeProvider.System)
    {
    }

    public void Mark()
    {
        var path = folders.SecureGaveUp();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, time.GetLocalNow().ToString("O", CultureInfo.InvariantCulture));
    }

    /// <summary>True if the watchdog gave up since Windows started.</summary>
    public bool IsActive()
    {
        try
        {
            var path = folders.SecureGaveUp();
            DateTimeOffset? gaveUpAt = File.Exists(path)
                && DateTimeOffset.TryParse(File.ReadAllText(path), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
                    ? at
                    : null;
            var bootTime = time.GetLocalNow() - TimeSpan.FromMilliseconds(Environment.TickCount64);
            return SecureDesktopMode.IsDisabledAfterCrashes(gaveUpAt, bootTime);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
