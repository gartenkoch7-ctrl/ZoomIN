using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>
/// ADR-0031 against the real machine stores: only as administrator (skipped otherwise). Signs a copy
/// of the test runner, checks the key's access list while it exists, and cleans up after itself.
/// </summary>
public sealed class LocalSignerAdminTests : IDisposable
{
    private readonly string _exe = Path.Combine(Path.GetTempPath(), $"zoomin-admin-sign-{Guid.NewGuid():N}.exe");

    [Fact]
    public void RealSigning_KeyOnlyForSystemAndAdmins_ValidSignature_NoKeyLeft()
    {
        Assert.SkipUnless(IsAdministrator(), "Needs administrator rights (LocalMachine certificate stores).");
        File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), _exe);
        var log = new RecordingLogger();
        string? access = null;
        string? thumbprint = null;
        string? keyName = null;
        var signer = new LocalSigner(
            log, TimeProvider.System, new InspectingProcess((t, a, k) => (thumbprint, access, keyName) = (t, a, k)), new LocalCertificateStore(log));
        try
        {
            Assert.True(signer.Sign(_exe), string.Join(" | ", log.Messages));

            // Only SYSTEM (SY) and Administrators (BA), protected (P): no user account can use the key.
            // The rights show as a mask (0xd01f01ff, measured 2026-11-17), so only the accounts count.
            Assert.Equal("D:P(A;;*;;;SY)(A;;*;;;BA)", System.Text.RegularExpressions.Regex.Replace(access ?? string.Empty, @"\(A;;[^;]+;;;", "(A;;*;;;"));

            // The named machine key itself is gone, not only the store entry pointing at it.
            Assert.StartsWith("ZoomIN-", keyName, StringComparison.Ordinal);
            Assert.False(CngKey.Exists(keyName!, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.MachineKey));
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            Assert.DoesNotContain(store.Certificates.Cast<X509Certificate2>(), c => c.Subject == TrustedPublishers.LocalInstall);

            // The signed file reads back with this certificate, so pruning keeps its trust. Prune
            // itself is not run here: it would also withdraw the installed ZoomIN's certificate.
            Assert.Equal(thumbprint, LocalSigner.SignerThumbprint(_exe), StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            // Only this test's certificate: the installed ZoomIN on this PC keeps its own.
            foreach (var name in (StoreName[])[StoreName.Root, StoreName.TrustedPublisher, StoreName.My])
            {
                using var store = new X509Store(name, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                foreach (var certificate in store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint ?? "-", false))
                {
                    store.Remove(certificate);
                }
            }
        }
    }

    public void Dispose() => File.Delete(_exe);

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    // Reads the access list of the key while it exists, then signs for real.
    private sealed class InspectingProcess(Action<string, string, string> seen) : ISigningProcess
    {
        public int Run(string executablePath, string thumbprint)
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                using var certificate = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false)[0];
                using var key = (RSACng)certificate.GetRSAPrivateKey()!;
                seen(thumbprint, LocalCertificateStore.AccessOf(certificate), key.Key.KeyName!);
            }

            return new SigningProcess().Run(executablePath, thumbprint);
        }
    }
}
