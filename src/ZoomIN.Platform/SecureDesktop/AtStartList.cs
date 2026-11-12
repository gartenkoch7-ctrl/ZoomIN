using Microsoft.Win32;
using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// The Ease of Access value "Configuration" below <paramref name="root"/>: the ATs Windows starts by
/// itself. Under HKCU after the user's sign-in (ADR-0030), under HKLM on the sign-in screen
/// (ADR-0016). Other names in the list (NVDA, Windows Magnifier) are never touched.
/// </summary>
/// <param name="root">HKCU or HKLM; tests pass a private key.</param>
public sealed class AtStartList(RegistryKey root)
{
    /// <summary>The same path below HKCU and HKLM (the registry ignores case).</summary>
    public const string KeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\Accessibility";

    private const string Value = "Configuration";

    public bool Contains(string name)
    {
        using var key = root.OpenSubKey(KeyPath);
        return AtConfigurationList.Contains(key?.GetValue(Value) as string, name);
    }

    /// <summary>Adds or removes <paramref name="name"/>; writes only if that changes the list.</summary>
    public void Set(string name, bool listed)
    {
        if (listed)
        {
            using var key = root.CreateSubKey(KeyPath);
            var list = key.GetValue(Value) as string;
            if (!AtConfigurationList.Contains(list, name))
            {
                key.SetValue(Value, AtConfigurationList.Add(list, name));
            }

            return;
        }

        using var existing = root.OpenSubKey(KeyPath, writable: true);
        if (existing?.GetValue(Value) is string current && AtConfigurationList.Contains(current, name))
        {
            existing.SetValue(Value, AtConfigurationList.Remove(current, name));
        }
    }
}
