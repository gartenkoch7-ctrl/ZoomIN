using Microsoft.Win32;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// Autostart of an installed ZoomIN through Ease of Access (ADR-0030): "ZoomIN" in the user's
/// "start after sign-in" list, HKCU "…\Accessibility\Configuration" (comma-separated AT names, as
/// NVDA does it). No admin rights; other names in the list stay untouched. Windows' own Settings
/// switch writes the same value, so <see cref="IsEnabled"/> always reads it instead of keeping a flag.
/// Needs the AT registration under HKLM "ATs\ZoomIN", which the elevated install step writes.
/// </summary>
public sealed class EaseOfAccessAutostart : IAutostart
{
    private readonly AtStartList _list;

    /// <param name="user">HKCU; tests pass a private key instead. Used through <see cref="AutostartSelector"/>,
    /// which keeps the Run key and the AT entry in line.</param>
    public EaseOfAccessAutostart(RegistryKey user)
    {
        _list = new AtStartList(user);
    }

    public bool IsEnabled => _list.Contains(SecureDesktopRegistration.AtName);

    public void SetEnabled(bool enabled) => _list.Set(SecureDesktopRegistration.AtName, enabled);
}
