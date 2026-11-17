using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// The LocalMachine certificate stores for <see cref="LocalSigner"/> (ADR-0031): only ZoomIN install
/// certificates are removed, never anything else; cleanup failures are logged, not thrown.
/// </summary>
internal sealed class LocalCertificateStore(ILogger log) : ICertificateStores
{
    public void Add(StoreName name, X509Certificate2 certificate) => Change(name, store => store.Add(certificate));

    public X509Certificate2 CreateInMy(DateTimeOffset now)
    {
        // Created as a machine key with its own access list, never imported: an imported key gets
        // the provider's default rights, which may include the (non-elevated) user's account, and
        // that account could sign with it while the certificate is trusted (review 2026-11-17).
        var parameters = new CngKeyCreationParameters
        {
            KeyCreationOptions = CngKeyCreationOptions.MachineKey,
            ExportPolicy = CngExportPolicies.None,
            Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
        };
        parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(3072), CngPropertyOptions.None));
        parameters.Parameters.Add(new CngProperty(SecurityDescriptorProperty, AdminsOnly(), DaclSecurityInformation));
        using var key = CngKey.Create(CngAlgorithm.Rsa, "ZoomIN-" + Guid.NewGuid().ToString("N"), parameters);
        try
        {
            using var rsa = new RSACng(key);

            // CreateSelfSigned returns a certificate bound to a copy of the key; the store needs the
            // link to the named machine key, so the public part is bound to it explicitly.
            using var selfSigned = LocalSigner.CreateCertificate(now, rsa);
            using var publicOnly = X509CertificateLoader.LoadCertificate(selfSigned.RawData);
            var withKey = publicOnly.CopyWithPrivateKey(rsa);
            Add(StoreName.My, withKey);
            return withKey;
        }
        catch
        {
            key.Delete();
            throw;
        }
    }

    /// <summary>The key's access list as Windows reports it (SDDL), for the admin-only integration test.</summary>
    public static string AccessOf(X509Certificate2 certificate)
    {
        using var rsa = (RSACng)certificate.GetRSAPrivateKey()!;
        var descriptor = rsa.Key.GetProperty(SecurityDescriptorProperty, DaclSecurityInformation).GetValue()!;
        return new System.Security.AccessControl.RawSecurityDescriptor(descriptor, 0).GetSddlForm(System.Security.AccessControl.AccessControlSections.Access);
    }

    // NCRYPT_SECURITY_DESCR_PROPERTY with DACL_SECURITY_INFORMATION as the property flag.
    private const string SecurityDescriptorProperty = "Security Descr";
    private const CngPropertyOptions DaclSecurityInformation = (CngPropertyOptions)4;

    // NTE_BAD_KEYSET: the key the certificate entry points at does not exist.
    private const int KeysetMissing = unchecked((int)0x80090016);

    // Protected DACL: full access for SYSTEM and Administrators only.
    private static byte[] AdminsOnly()
    {
        var descriptor = new System.Security.AccessControl.RawSecurityDescriptor("D:P(A;;GA;;;SY)(A;;GA;;;BA)");
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }

    public int Remove(StoreName name, Func<X509Certificate2, bool> remove, bool deleteKeys)
    {
        try
        {
            var count = 0;
            Change(name, store => count = RemoveMatching(store, remove, deleteKeys));
            return count;
        }
        catch (CryptographicException ex)
        {
            log.CertificateStoreUnchanged(name.ToString(), ex.GetType().Name);
            return 0;
        }
    }

    public bool DeleteKey(X509Certificate2 certificate)
    {
        try
        {
            using var key = certificate.GetRSAPrivateKey();
            if (key is RSACng cng)
            {
                cng.Key.Delete();
            }
            else if (key is RSACryptoServiceProvider csp)
            {
                csp.PersistKeyInCsp = false;
            }

            return true;
        }
        catch (CryptographicException ex) when (ex.HResult == KeysetMissing)
        {
            // The entry still names a key that is gone (deleted, then the store change failed):
            // nothing left to delete, so the entry may go.
            return true;
        }
        catch (CryptographicException ex)
        {
            log.CertificateStoreUnchanged("key", ex.GetType().Name);
            return false;
        }
    }

    /// <summary>Only a ZoomIN install certificate the caller wants gone; Windows' own roots never match.</summary>
    internal static bool Selects(X509Certificate2 certificate, Func<X509Certificate2, bool> remove) =>
        string.Equals(certificate.Subject, TrustedPublishers.LocalInstall, StringComparison.OrdinalIgnoreCase) && remove(certificate);

    // With deleteKeys, an entry whose key cannot be deleted stays, so a later run can try again;
    // it never ends up as a key nobody can find.
    private int RemoveMatching(X509Store store, Func<X509Certificate2, bool> remove, bool deleteKeys)
    {
        var count = 0;
        foreach (var certificate in store.Certificates)
        {
            using (certificate)
            {
                if (!Selects(certificate, remove))
                {
                    continue;
                }

                if (deleteKeys && certificate.HasPrivateKey && !DeleteKey(certificate))
                {
                    continue;
                }

                store.Remove(certificate);
                count++;
            }
        }

        return count;
    }

    private static void Change(StoreName name, Action<X509Store> change)
    {
        using var store = new X509Store(name, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        change(store);
    }
}
