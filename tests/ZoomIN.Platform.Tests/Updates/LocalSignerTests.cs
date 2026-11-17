using System.Security.Cryptography.X509Certificates;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>
/// ADR-0031: the certificate the install step trusts machine-wide can only sign code and can never
/// issue other certificates. Signing itself needs administrator rights and is checked in the field test.
/// </summary>
public sealed class LocalSignerTests
{
    [Fact]
    public void Certificate_IsCodeSigningOnly_AndNoCa()
    {
        var now = DateTimeOffset.Now;
        using var certificate = LocalSigner.CreateCertificate(now);

        Assert.Equal(TrustedPublishers.LocalInstall, certificate.Subject);
        Assert.Equal(certificate.Subject, certificate.Issuer);
        var basic = Assert.Single(certificate.Extensions.OfType<X509BasicConstraintsExtension>());
        Assert.False(basic.CertificateAuthority);
        Assert.True(basic.Critical);
        var usage = Assert.Single(certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>());
        Assert.Equal(["1.3.6.1.5.5.7.3.3"], usage.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>().Select(o => o.Value));
        Assert.Equal(X509KeyUsageFlags.DigitalSignature, Assert.Single(certificate.Extensions.OfType<X509KeyUsageExtension>()).KeyUsages);
        Assert.Equal(3072, certificate.GetRSAPublicKey()!.KeySize);
        Assert.True(certificate.NotBefore < now.LocalDateTime && certificate.NotAfter > now.AddYears(49).LocalDateTime);
    }

    [Fact]
    public void StoreCleanup_SelectsOnlyZoomInInstallCertificates()
    {
        using var own = LocalSigner.CreateCertificate(DateTimeOffset.Now);
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        // An installed ZoomIN on this PC has its own certificate there; every other root must stay.
        var windowsRoots = store.Certificates.Cast<X509Certificate2>().Where(c => c.Issuer != TrustedPublishers.LocalInstall).ToList();

        Assert.True(LocalCertificateStore.Selects(own, _ => true));
        Assert.False(LocalCertificateStore.Selects(own, _ => false));
        Assert.NotEmpty(windowsRoots);
        Assert.DoesNotContain(windowsRoots, root => LocalCertificateStore.Selects(root, _ => true));
        windowsRoots.ForEach(root => root.Dispose());
    }
}
