using System.Diagnostics;
using System.Reflection;
using ZoomIN.App.Settings;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.App.Updates;

/// <summary>
/// Release list, update check and one-click install (ADR-0014/0015): downloads and verifies the
/// package as the user, backs up settings before a downgrade, runs the installed ZoomIN.exe
/// elevated for the Program Files part and restarts into the new version.
/// </summary>
public sealed class UpdateService
{
    private static readonly TimeSpan s_checkInterval = TimeSpan.FromDays(1);

    private readonly IReleaseFeed _feed;
    private readonly IPackageDownloader _downloader;
    private readonly ISettingsBackup _backup;
    private readonly SettingsService _settings;
    private readonly ILogger _log;
    private readonly IAppFolders _folders;
    private readonly IElevatedStep _elevated;
    private readonly InstallLayout? _layout;
    private AppVersion? _notified;

    // The last installation: the restart that follows continues its trace (zoomin.update.restart).
    private ActivityContext _installed;

    public UpdateService(
        IReleaseFeed feed, IPackageDownloader downloader, ISettingsBackup backup, SettingsService settings, IAppFolders folders,
        IElevatedStep elevated, ILogger<UpdateService> log)
    {
        _folders = folders;
        _elevated = elevated;
        _feed = feed;
        _downloader = downloader;
        _backup = backup;
        _settings = settings;
        _log = log;
        _layout = InstallLayout.TryFromExecutable(Environment.ProcessPath, out var layout) ? layout : null;
        CurrentVersion = ReadCurrentVersion();
    }

    /// <summary>Raised (on a worker thread) when the daily check finds a newer release, once per version.</summary>
    public event Action<ReleaseInfo>? UpdateAvailable;

    public AppVersion CurrentVersion { get; }

    /// <summary>False for developer builds and the portable ZIP: only an installed ZoomIN can update itself.</summary>
    public bool CanInstall => _layout is not null;

    public IReadOnlyList<ReleaseInfo> Releases { get; private set; } = [];

    public ReleaseChannel Channel => _settings.Current.UpdateChannel;

    public IReadOnlyList<ReleaseInfo> Offered => ReleaseCatalog.Offered(Releases, Channel);

    public ReleaseInfo? AvailableUpdate => ReleaseCatalog.Update(Releases, Channel, CurrentVersion);

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var operation = UpdateTelemetry.Start("zoomin.update.check")?.SetTag("zoomin.update.channel", Channel.ToString());
        Releases = await _feed.ReleasesAsync(ReleaseFeeds.GitHub, cancellationToken).ConfigureAwait(false);
        operation?.SetTag("zoomin.update.releases", Releases.Count);
    }

    /// <summary>Checks 10 s after start and then daily, as long as the user allows it.</summary>
    public async Task RunChecksAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_settings.Current.CheckForUpdates)
            {
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
                if (AvailableUpdate is { } update && update.Version != _notified)
                {
                    _notified = update.Version;
                    _log.UpdateAvailable(update.Version);
                    UpdateAvailable?.Invoke(update);
                }
            }

            await Task.Delay(s_checkInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<UpdateResult> InstallAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        using var operation = UpdateTelemetry.Start("zoomin.update.install", release.Version);
        var result = await InstallPackageAsync(release, progress, cancellationToken).ConfigureAwait(false);
        _installed = operation?.Context ?? default;
        return operation.Finish(result, UpdateTelemetry.Succeeded(result));
    }

    private async Task<UpdateResult> InstallPackageAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (_layout is null)
        {
            return UpdateResult.NotInstalled;
        }

        var folder = _folders.Updates();
        var download = await _downloader.DownloadAsync(release, folder, progress, cancellationToken).ConfigureAwait(false);
        if (download.Path is null)
        {
            return download.Result is PackageCheck.ChecksumMismatch or PackageCheck.ChecksumMissing
                ? UpdateResult.PackageMismatch
                : UpdateResult.Failed;
        }

        try
        {
            if (ReleaseCatalog.Classify(CurrentVersion, release.Version) == UpdateKind.Downgrade)
            {
                using var backup = UpdateTelemetry.Start("zoomin.update.backup", CurrentVersion);
                _backup.Backup(CurrentVersion);
            }

            var hash = await Task.Run(() => _elevated.HashOf(download.Path), cancellationToken).ConfigureAwait(false);
            var command = new UpdateCommand(download.Path, hash, release.Version);
            return await Task.Run(() => _elevated.Run(_layout.CurrentExecutable, command), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDelete(download.Path);
        }
    }

    /// <summary>Starts the newly installed version, which waits until this process has ended.</summary>
    public void StartNewVersion()
    {
        if (_layout is null)
        {
            return;
        }

        // A child of the installation, so the new version's start belongs to the same trace.
        using var operation = Telemetry.Source.StartActivity("zoomin.update.restart", ActivityKind.Internal, _installed);

        // Through the shell: CreateProcess refuses UIAccess executables (ADR-0012).
        using var _ = Process.Start(new ProcessStartInfo(_layout.CurrentExecutable, TraceParentArgument.Append(RestartArguments.Format(Environment.ProcessId)))
        {
            UseShellExecute = true,
        });
    }

    private static AppVersion ReadCurrentVersion()
    {
        var informational = typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return AppVersion.TryParse(informational, out var version) ? version : new AppVersion(0, 0, 0);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Overwritten by the next download.
        }
    }
}
