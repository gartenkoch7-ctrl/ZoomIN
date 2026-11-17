using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// HTTP client for the elevated install step (ADR-0031): a server certificate counts only if its
/// root is trusted by the machine. The elevated process runs as the same user, whose own root store
/// (CurrentUser\Root) and proxy settings can be changed without admin rights; a root added there
/// would let a man in the middle hand out a forged checksums.txt (review 2026-11-17).
/// </summary>
public sealed class MachineTrustHttpClient : HttpClient
{
    public MachineTrustHttpClient()
        : base(new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = RootTrustedByMachine },
        })
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZoomIN", "1.0"));
    }

    internal static bool RootTrustedByMachine(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors != SslPolicyErrors.None || chain is null || chain.ChainElements.Count == 0)
        {
            return false;
        }

        var root = chain.ChainElements[^1].Certificate;

        // Root: added by an administrator; AuthRoot: Windows' own root program, filled on demand.
        return InStore(StoreName.Root, root) || InStore(StoreName.AuthRoot, root);
    }

    private static bool InStore(StoreName name, X509Certificate2 root)
    {
        using var store = new X509Store(name, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var found = store.Certificates.Find(X509FindType.FindByThumbprint, root.Thumbprint, validOnly: false);
        var trusted = found.Count > 0;
        foreach (var certificate in found)
        {
            certificate.Dispose();
        }

        return trusted;
    }
}
