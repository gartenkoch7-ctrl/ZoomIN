using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.App.Updates;

/// <summary>
/// Installs the signed build where ZoomIN runs without UIAccess (developer build, portable ZIP;
/// ADR-0017): downloads the setup program of the newest release in the user's channel, checks it
/// against the release's SHA-256 list and the pinned ZoomIN signer, then starts it. The setup asks
/// for administrator rights itself and ends this ZoomIN while installing. No certificate is ever
/// created or trusted on the user's PC.
/// </summary>
public sealed class SetupLauncher
{
    private readonly UpdateService _updates;
    private readonly IPackageDownloader _downloader;
    private readonly ISignatureVerifier _verifier;
    private readonly ILogger _log;

    public SetupLauncher(UpdateService updates, IPackageDownloader downloader, ISignatureVerifier verifier, ILogger<SetupLauncher> log)
    {
        _updates = updates;
        _downloader = downloader;
        _verifier = verifier;
        _log = log;
    }

    public async Task<SetupLaunch> LaunchAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        // The setup started here continues this operation (--traceparent).
        using var operation = UpdateTelemetry.Start("zoomin.setup.launch");
        var launch = await LaunchSetupAsync(progress, cancellationToken).ConfigureAwait(false);
        return operation.Finish(launch, launch == SetupLaunch.Started);
    }

    private async Task<SetupLaunch> LaunchSetupAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        try
        {
            await _updates.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            _log.SetupReleaseListUnavailable(ex.Message);
            return SetupLaunch.DownloadFailed;
        }

        if (ReleaseCatalog.NewestSetup(_updates.Releases, _updates.Channel) is not { } release)
        {
            return SetupLaunch.NoRelease;
        }

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN", "setup");
        var download = await _downloader.DownloadSetupAsync(release, folder, progress, cancellationToken).ConfigureAwait(false);
        if (download.Path is null)
        {
            return SetupLaunch.DownloadFailed;
        }

        // Until the public signature (SignPath) exists, releases carry no setup Windows would trust.
        var check = _verifier.Verify(download.Path);
        if (check != PackageCheck.Ok)
        {
            _log.SetupNotStarted(release.Version, check.ToString());
            return SetupLaunch.NotTrusted;
        }

        try
        {
            using var _ = Process.Start(new ProcessStartInfo(download.Path, TraceParentArgument.Append(string.Empty)) { UseShellExecute = true });
            _log.SetupStarted(release.Version);
            return SetupLaunch.Started;
        }
        catch (Win32Exception ex)
        {
            _log.SetupNotStarted(release.Version, ex.Message);
            return SetupLaunch.DownloadFailed;
        }
    }
}
