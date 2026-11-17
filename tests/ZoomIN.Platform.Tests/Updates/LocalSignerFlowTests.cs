using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>
/// ADR-0031: what the install step leaves trusted on each path. Fake stores and a fake signing
/// process, so no test touches the machine's certificates (the real stores need admin rights).
/// </summary>
public sealed class LocalSignerFlowTests
{
    private const string Exe = @"C:\Program Files\ZoomIN\app-1.0.0.staging\ZoomIN.exe";

    private readonly FakeStores _stores = new();
    private readonly FakeProcess _process = new();

    [Fact]
    public void Signed_TrustsTheCertificate_AndLeavesNoKeyOrEntryInMy()
    {
        Assert.True(Signer().Sign(Exe));

        var thumbprint = Assert.Single(_stores.In(StoreName.Root));
        Assert.Equal([thumbprint], _stores.In(StoreName.TrustedPublisher));
        Assert.Empty(_stores.In(StoreName.My));
        Assert.Equal([thumbprint], _stores.KeysDeleted);
        Assert.Equal((Exe, thumbprint), _process.Calls.Single());
    }

    [Fact]
    public void SigningFails_WithdrawsTheTrust()
    {
        _process.ExitCode = 2;

        Assert.False(Signer().Sign(Exe));

        Assert.Empty(_stores.In(StoreName.Root));
        Assert.Empty(_stores.In(StoreName.TrustedPublisher));
        Assert.Single(_stores.KeysDeleted);
    }

    [Fact]
    public void TrustedPublisherRefusesTheCertificate_RootTrustIsWithdrawn_AndTheKeyDeleted()
    {
        _stores.FailingStore = StoreName.TrustedPublisher;

        Assert.False(Signer().Sign(Exe));

        Assert.Empty(_stores.In(StoreName.Root));
        Assert.Empty(_stores.In(StoreName.My));
        Assert.Single(_stores.KeysDeleted);
        Assert.Empty(_process.Calls);
    }

    [Fact]
    public void KeyCannotBeDeleted_IsAFailure_AndTheEntryStaysForTheNextRun()
    {
        _stores.KeyDeletionWorks = false;

        Assert.False(Signer().Sign(Exe));

        Assert.Empty(_stores.In(StoreName.Root));
        Assert.Single(_stores.In(StoreName.My));
    }

    [Fact]
    public void Prune_WithAnUnreadableSigner_KeepsEverything()
    {
        Assert.True(Signer().Sign(Exe));

        Signer().Prune([Path.Combine(Path.GetTempPath(), "no-such-zoomin.exe")]);

        Assert.Single(_stores.In(StoreName.Root));
    }

    [Fact]
    public void Prune_RemovesCertificatesNoKeptVersionIsSignedWith()
    {
        Assert.True(Signer().Sign(Exe));

        // Signed by Microsoft, not by the certificate just created: nothing kept needs it.
        Signer().Prune([typeof(object).Assembly.Location]);

        Assert.Empty(_stores.In(StoreName.Root));
        Assert.Empty(_stores.In(StoreName.TrustedPublisher));
    }

    [Fact]
    public void RemoveAll_ClearsEveryStore()
    {
        Assert.True(Signer().Sign(Exe));

        var log = new RecordingLogger();
        new LocalSigner(log, TimeProvider.System, _process, _stores).RemoveAll();

        Assert.Empty(_stores.In(StoreName.Root));
        Assert.Empty(_stores.In(StoreName.TrustedPublisher));
        Assert.Empty(_stores.In(StoreName.My));
        Assert.Contains(log.Messages, m => m.StartsWith("2615 Removed 2 ", StringComparison.Ordinal));
    }

    private LocalSigner Signer() => new(NullLogger.Instance, TimeProvider.System, _process, _stores);

    private sealed class FakeProcess : ISigningProcess
    {
        public int ExitCode { get; set; }

        public List<(string Path, string Thumbprint)> Calls { get; } = [];

        public int Run(string executablePath, string thumbprint)
        {
            Calls.Add((executablePath, thumbprint));
            return ExitCode;
        }
    }

    private sealed class FakeStores : ICertificateStores
    {
        private readonly Dictionary<StoreName, List<X509Certificate2>> _stores = [];

        public bool KeyDeletionWorks { get; set; } = true;

        public List<string> KeysDeleted { get; } = [];

        public List<string> In(StoreName name) => [.. Store(name).Select(c => c.Thumbprint)];

        public StoreName? FailingStore { get; set; }

        public void Add(StoreName name, X509Certificate2 certificate) =>
            Store(name == FailingStore ? throw new System.Security.Cryptography.CryptographicException("store refused") : name).Add(X509CertificateLoader.LoadCertificate(certificate.RawData));

        public X509Certificate2 CreateInMy(DateTimeOffset now)
        {
            using var created = LocalSigner.CreateCertificate(now);
            Add(StoreName.My, created);
            return X509CertificateLoader.LoadCertificate(created.RawData);
        }

        public int Remove(StoreName name, Func<X509Certificate2, bool> remove, bool deleteKeys)
        {
            var matching = Store(name)
                .Where(c => c.Subject == TrustedPublishers.LocalInstall && remove(c) && (!deleteKeys || DeleteKey(c)))
                .ToList();
            Store(name).RemoveAll(matching.Contains);
            return matching.Count;
        }

        public bool DeleteKey(X509Certificate2 certificate)
        {
            if (KeyDeletionWorks)
            {
                KeysDeleted.Add(certificate.Thumbprint);
            }

            return KeyDeletionWorks;
        }

        private List<X509Certificate2> Store(StoreName name) =>
            _stores.TryGetValue(name, out var store) ? store : _stores[name] = [];
    }
}
