using System.Diagnostics;
using System.Reflection;
using ZoomIN.App.Settings;
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
    private readonly ILog _log;
    private readonly InstallLayout? _layout;
    private AppVersion? _notified;

    public UpdateService(
        IReleaseFeed feed, IPackageDownloader downloader, ISettingsBackup backup, SettingsService settings, ILog log)
    {
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
        Releases = await _feed.ReleasesAsync(ReleaseFeeds.GitHub, cancellationToken).ConfigureAwait(false);
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
                    _log.Info($"Update available: {update.Version}");
                    UpdateAvailable?.Invoke(update);
                }
            }

            await Task.Delay(s_checkInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<UpdateResult> InstallAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (_layout is null)
        {
            return UpdateResult.NotInstalled;
        }

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN", "updates");
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
                _backup.Backup(CurrentVersion);
            }

            var hash = await Task.Run(() => ElevatedStep.HashOf(download.Path), cancellationToken).ConfigureAwait(false);
            var command = new UpdateCommand(download.Path, hash, release.Version);
            return await Task.Run(() => ElevatedStep.Run(_layout.CurrentExecutable, command, _log), cancellationToken).ConfigureAwait(false);
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

        // Through the shell: CreateProcess refuses UIAccess executables (ADR-0012).
        using var _ = Process.Start(new ProcessStartInfo(_layout.CurrentExecutable, RestartArguments.Format(Environment.ProcessId))
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
