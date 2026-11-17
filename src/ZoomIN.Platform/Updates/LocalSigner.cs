using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Signs the installed ZoomIN.exe on this PC (ADR-0031). A fresh self-signed certificate, code
/// signing only and no CA, is trusted machine-wide; the exe is signed with it; then its private key
/// is deleted, so nothing can be signed with it again. Needs administrator rights (elevated step).
/// Every failure path deletes the key and withdraws the trust it added; nothing here throws.
/// </summary>
public sealed class LocalSigner : ILocalSigner
{
    private const string CodeSigningOid = "1.3.6.1.5.5.7.3.3";

    private readonly ILogger _log;
    private readonly TimeProvider _clock;
    private readonly ISigningProcess _process;
    private readonly ICertificateStores _stores;

    private static readonly StoreName[] s_trustStores = [StoreName.Root, StoreName.TrustedPublisher];

    public LocalSigner(ILogger log, TimeProvider clock)
        : this(log, clock, new SigningProcess(), new LocalCertificateStore(log))
    {
    }

    internal LocalSigner(ILogger log, TimeProvider clock, ISigningProcess process, ICertificateStores stores)
    {
        _log = log;
        _clock = clock;
        _process = process;
        _stores = stores;
    }

    /// <summary>The certificate for one install: valid from yesterday (clock skew) for 50 years.</summary>
    public static X509Certificate2 CreateCertificate(DateTimeOffset now)
    {
        using var rsa = RSA.Create(3072);
        return CreateCertificate(now, rsa);
    }

    /// <summary>The same, for a given key (the persisted machine key of the install step).</summary>
    public static X509Certificate2 CreateCertificate(DateTimeOffset now, RSA rsa)
    {
        var request = new CertificateRequest(TrustedPublishers.LocalInstall, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(CodeSigningOid)], true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(50));
    }

    public bool Sign(string executablePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        using var operation = Telemetry.Source.StartActivity("zoomin.update.sign");

        // A step killed in the middle (power loss) may have left a key behind: never keep one.
        _stores.Remove(StoreName.My, _ => true, deleteKeys: true);
        string? thumbprint = null;
        var signed = false;
        try
        {
            var exitCode = WithKeyInStore(created =>
            {
                thumbprint = created.Thumbprint;
                using (var publicOnly = X509CertificateLoader.LoadCertificate(created.RawData))
                {
                    foreach (var name in s_trustStores)
                    {
                        _stores.Add(name, publicOnly);
                    }
                }

                return _process.Run(executablePath, created.Thumbprint);
            }, out var keyDeleted);
            signed = exitCode == 0 && keyDeleted;
            if (exitCode != 0)
            {
                _log.LocalSigningFailed("powershell " + exitCode);
            }
            else if (!keyDeleted)
            {
                // A trusted certificate whose key still exists must not be used: untrusted below, and
                // its store entry stays so the next run can delete the key.
                _log.LocalSigningFailed("key not deleted");
            }
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _log.LocalSigningFailed(ex.GetType().Name);
        }

        if (!signed)
        {
            // A certificate nothing is signed with must not stay trusted.
            Untrust(thumbprint);
            operation?.SetStatus(ActivityStatusCode.Error, "not signed");
            return false;
        }

        _log.LocallySigned(thumbprint!);
        return true;
    }

    public void Prune(IEnumerable<string> keptExecutables)
    {
        ArgumentNullException.ThrowIfNull(keptExecutables);
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var executable in keptExecutables)
        {
            // An unreadable signer might be the active version's: then rather keep everything.
            if (SignerThumbprint(executable) is not { } thumbprint)
            {
                _log.CertificateCleanupSkipped("signer unreadable");
                return;
            }

            keep.Add(thumbprint);
        }

        var removed = 0;
        foreach (var name in s_trustStores)
        {
            removed += _stores.Remove(name, c => !keep.Contains(c.Thumbprint), deleteKeys: false);
        }

        if (removed > 0)
        {
            _log.OldCertificatesRemoved(removed);
        }
    }

    public void RemoveAll()
    {
        var removed = _stores.Remove(StoreName.My, _ => true, deleteKeys: true);
        foreach (var name in s_trustStores)
        {
            removed += _stores.Remove(name, _ => true, deleteKeys: false);
        }

        _log.AllCertificatesRemoved(removed);
    }

    // The signing step runs in another process, so the key lives in the machine store only for it.
    // Whatever happens, the finally deletes the key; only then the store entry goes too.
    private int WithKeyInStore(Func<X509Certificate2, int> sign, out bool keyDeleted)
    {
        keyDeleted = true;
        var stored = _stores.CreateInMy(_clock.GetLocalNow());
        try
        {
            return sign(stored);
        }
        finally
        {
            keyDeleted = _stores.DeleteKey(stored);
            var thumbprint = stored.Thumbprint;
            stored.Dispose();
            if (keyDeleted)
            {
                // The key is gone already (deleting it again would only fail and log): the entry only.
                _stores.Remove(StoreName.My, c => c.Thumbprint == thumbprint, deleteKeys: false);
            }
        }
    }

    private void Untrust(string? thumbprint)
    {
        if (thumbprint is null)
        {
            return;
        }

        foreach (var name in s_trustStores)
        {
            _stores.Remove(name, c => string.Equals(c.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase), deleteKeys: false);
        }
    }

    internal static string? SignerThumbprint(string executable)
    {
        try
        {
#pragma warning disable SYSLIB0057 // The replacement (X509CertificateLoader) cannot read signer certificates of a PE file.
            using var raw = X509Certificate.CreateFromSignedFile(executable);
#pragma warning restore SYSLIB0057
            using var signer = X509CertificateLoader.LoadCertificate(raw.Export(X509ContentType.Cert));
            return signer.Thumbprint;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
