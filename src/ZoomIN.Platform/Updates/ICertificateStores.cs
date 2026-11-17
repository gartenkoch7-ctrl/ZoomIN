using System.Security.Cryptography.X509Certificates;

namespace ZoomIN.Platform.Updates;

/// <summary>The machine certificate stores as <see cref="LocalSigner"/> uses them (ADR-0031); a fake in tests.</summary>
internal interface ICertificateStores
{
    /// <summary>Adds the public certificate to <paramref name="name"/>; throws on failure, so the caller stops.</summary>
    void Add(StoreName name, X509Certificate2 certificate);

    /// <summary>
    /// Creates the install certificate with a new persisted machine key that only SYSTEM and
    /// Administrators may use, and puts it into LocalMachine\My for the signing process; throws on failure.
    /// </summary>
    X509Certificate2 CreateInMy(DateTimeOffset now);

    /// <summary>Removes the ZoomIN install certificates <paramref name="remove"/> picks; never throws, returns how many.</summary>
    int Remove(StoreName name, Func<X509Certificate2, bool> remove, bool deleteKeys);

    /// <returns>True if the persisted private key is gone (or there was none).</returns>
    bool DeleteKey(X509Certificate2 certificate);
}
