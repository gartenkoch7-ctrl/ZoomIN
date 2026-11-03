using System.Globalization;
using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Watchdog.Crashes;

/// <summary>
/// Remembers that the watchdog gave up on the secure-desktop instance (ADR-0016). Stored next to
/// the crash log, which for SYSTEM is in SYSTEM's own profile.
/// </summary>
public static class SecureDesktopGiveUp
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN", "secure-gave-up.txt");

    public static void Mark()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
    }

    /// <summary>True if the watchdog gave up since Windows started.</summary>
    public static bool IsActive()
    {
        try
        {
            DateTimeOffset? gaveUpAt = File.Exists(FilePath)
                && DateTimeOffset.TryParse(File.ReadAllText(FilePath), CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                    ? time
                    : null;
            var bootTime = DateTimeOffset.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
            return SecureDesktopMode.IsDisabledAfterCrashes(gaveUpAt, bootTime);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
