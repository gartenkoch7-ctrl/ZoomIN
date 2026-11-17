using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>
/// ADR-0031: the elevated step trusts a server only through a root the machine trusts – a root the
/// user added to their own store (no admin rights needed) must not count.
/// </summary>
public sealed class MachineTrustHttpClientTests
{
    [Fact]
    public void ChainErrors_AreRefused()
    {
        using var root = MachineRoot();
        using var chain = ChainTo(root);

        Assert.False(MachineTrustHttpClient.RootTrustedByMachine(this, root, chain, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void RootTheMachineTrusts_IsAccepted()
    {
        using var root = MachineRoot();
        using var chain = ChainTo(root);

        Assert.True(MachineTrustHttpClient.RootTrustedByMachine(this, root, chain, SslPolicyErrors.None));
    }

    [Fact]
    public void RootOnlyTheUserTrusts_IsRefused()
    {
        // Stands for a root in CurrentUser\Root: the user's chain is fine, the machine never saw it.
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=ZoomIN Test User Root", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var root = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
        using var chain = ChainTo(root);

        Assert.False(MachineTrustHttpClient.RootTrustedByMachine(this, root, chain, SslPolicyErrors.None));
    }

    [Fact]
    public void NoChain_IsRefused() =>
        Assert.False(MachineTrustHttpClient.RootTrustedByMachine(this, null, null, SslPolicyErrors.None));

    private static X509Certificate2 MachineRoot()
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        return store.Certificates.Cast<X509Certificate2>().First(c => c.Subject == c.Issuer);
    }

    private static X509Chain ChainTo(X509Certificate2 root)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
        chain.Build(root);
        return chain;
    }
}
