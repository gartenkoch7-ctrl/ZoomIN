using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.WinTrust;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Asks Windows (WinVerifyTrust) whether an executable's embedded signature is intact and chains
/// to a trusted root, then pins the signer to <see cref="TrustedPublishers"/> (ADR-0014).
/// </summary>
public sealed unsafe class AuthenticodeVerifier : ISignatureVerifier
{
    // HRESULTs from WinTrust.h meaning "nothing to verify" rather than "verification failed":
    // no signature, or a file type (zip, text) that cannot carry one.
    private const int TrustNoSignature = unchecked((int)0x800B0100);
    private const int TrustSubjectFormUnknown = unchecked((int)0x800B0003);
    private const int TrustProviderUnknown = unchecked((int)0x800B0001);

    public PackageCheck Verify(string executablePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        var trust = VerifyTrust(Path.GetFullPath(executablePath));
        if (trust != PackageCheck.Ok)
        {
            return trust;
        }

        return TrustedPublishers.IsTrusted(SignerSubject(executablePath)) ? PackageCheck.Ok : PackageCheck.UnexpectedPublisher;
    }

    private static PackageCheck VerifyTrust(string path)
    {
        fixed (char* file = path)
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)sizeof(WINTRUST_FILE_INFO),
                pcwszFilePath = file,
            };
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)sizeof(WINTRUST_DATA),
                dwUIChoice = WINTRUST_DATA_UICHOICE.WTD_UI_NONE,

                // The local development certificate has no revocation list, so an online check would
                // always fail; the pinned signer is what keeps foreign files out.
                fdwRevocationChecks = WINTRUST_DATA_REVOCATION_CHECKS.WTD_REVOKE_NONE,
                dwUnionChoice = WINTRUST_DATA_UNION_CHOICE.WTD_CHOICE_FILE,
                dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_VERIFY,
            };
            data.Anonymous.pFile = &fileInfo;

            var action = PInvoke.WINTRUST_ACTION_GENERIC_VERIFY_V2;
            var result = PInvoke.WinVerifyTrust(HWND.Null, &action, &data);

            // Releases the state WinVerifyTrust allocated for the verification above.
            data.dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_CLOSE;
            _ = PInvoke.WinVerifyTrust(HWND.Null, &action, &data);

            return result switch
            {
                0 => PackageCheck.Ok,
                TrustNoSignature or TrustSubjectFormUnknown or TrustProviderUnknown => PackageCheck.NotSigned,
                _ => PackageCheck.SignatureInvalid,
            };
        }
    }

    private static string? SignerSubject(string path)
    {
        try
        {
#pragma warning disable SYSLIB0057 // The replacement (X509CertificateLoader) cannot read signer certificates of a PE file.
            using var certificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            return certificate.Subject;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
