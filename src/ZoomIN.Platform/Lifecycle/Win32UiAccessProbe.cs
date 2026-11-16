using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Security;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// Reads TokenUIAccess of the own process token (ADR-0012). Windows sets it only for a signed,
/// trusted exe with uiAccess="true" started from a protected folder such as Program Files; a
/// developer build, the portable ZIP or an untrusted signature all report false.
/// </summary>
public sealed unsafe class Win32UiAccessProbe : IUiAccessProbe
{
    private readonly Lazy<bool> _isActive = new(Read);

    public bool IsActive => _isActive.Value;

    private static bool Read()
    {
        using var identity = WindowsIdentity.GetCurrent();
        uint uiAccess = 0;
        return PInvoke.GetTokenInformation(
            identity.AccessToken, TOKEN_INFORMATION_CLASS.TokenUIAccess, &uiAccess, sizeof(uint), out _) && uiAccess != 0;
    }
}
