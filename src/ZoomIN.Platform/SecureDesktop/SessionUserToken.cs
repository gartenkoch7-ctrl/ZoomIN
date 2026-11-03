using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// Finds the token of the user signed in to a session, from the secure-desktop instance (ADR-0016).
/// WTSQueryUserToken needs SeTcbPrivilege, which the SYSTEM token that Windows' Ease of Access gives
/// a registered AT does not hold (observed 2026-11-03: error 1314 on lock screen and UAC). The
/// fallback is the token of the user's shell (explorer.exe) in that session: it is the user's own
/// token, so impersonating it grants no more than the user already has.
/// </summary>
internal static unsafe class SessionUserToken
{
    private const int PrivilegeNotHeld = 1314;

    public static SafeAccessTokenHandle? Open(uint session, ILog log)
    {
        HANDLE raw;
        if (PInvoke.WTSQueryUserToken(session, &raw))
        {
            return new SafeAccessTokenHandle((nint)raw.Value);
        }

        var error = Marshal.GetLastPInvokeError();
        if (error != PrivilegeNotHeld)
        {
            // ERROR_NO_TOKEN while nobody is signed in to the session (sign-in after boot).
            log.Info($"Secure desktop: no signed-in user in session {session} (error {error})");
            return null;
        }

        var shell = FromShell(session);
        log.Info(shell is null
            ? $"Secure desktop: no user shell in session {session}"
            : "Secure desktop: using the token of the user's shell");
        return shell;
    }

    private static SafeAccessTokenHandle? FromShell(uint session)
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            using (process)
            {
                if (process.SessionId == session && TryOpenToken(process.Id) is { } token)
                {
                    return token;
                }
            }
        }

        return null;
    }

    private static SafeAccessTokenHandle? TryOpenToken(int processId)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
        if (process.IsInvalid)
        {
            return null;
        }

        // Query + impersonate is all SHGetKnownFolderPath and RunImpersonated need; duplicate lets
        // RunImpersonated make its own impersonation copy of this primary token.
        const TOKEN_ACCESS_MASK Access = TOKEN_ACCESS_MASK.TOKEN_QUERY | TOKEN_ACCESS_MASK.TOKEN_IMPERSONATE | TOKEN_ACCESS_MASK.TOKEN_DUPLICATE;
        if (!PInvoke.OpenProcessToken(process, Access, out var token))
        {
            return null;
        }

        var handle = new SafeAccessTokenHandle(token.DangerousGetHandle());
        token.SetHandleAsInvalid();
        return handle;
    }
}
