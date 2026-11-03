using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.Settings;
using ZoomIN.Platform.Shell;
using ZoomIN.Platform.Updates;
using ZoomIN.Setup.Localization;

namespace ZoomIN.Setup.Install;

/// <summary>
/// The unelevated part of the setup (ADR-0014): removes the old Inno installation, downloads and
/// verifies the chosen version, runs this setup elevated for Program Files and then applies the
/// user's choices as the signed-in user (autostart, desktop, settings).
/// </summary>
internal sealed class SetupInstaller
{
    private readonly ILog _log;
    private readonly HttpPackageDownloader _downloader;

    public SetupInstaller(ILog log)
    {
        _log = log;
        _downloader = new HttpPackageDownloader(log);
    }

    public async Task<UpdateResult> InstallAsync(
        ReleaseInfo release, SetupChoices choices, IProgress<string> status, IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(status);
        status.Report(SetupText.Get("Legacy"));
        if (await Task.Run(() => InstallRegistration.RemoveLegacyInstall(TimeSpan.FromSeconds(60)), cancellationToken))
        {
            _log.Info("[Setup] Old Inno installation removed.");
        }

        status.Report(SetupText.Format("Downloading", release.Version));
        var folder = Path.Combine(Path.GetTempPath(), "ZoomIN-Setup");
        var download = await _downloader.DownloadAsync(release, folder, progress, cancellationToken);
        if (download.Path is null)
        {
            return download.Result is PackageCheck.ChecksumMismatch or PackageCheck.ChecksumMissing
                ? UpdateResult.PackageMismatch
                : UpdateResult.Failed;
        }

        try
        {
            status.Report(SetupText.Format("Installing", release.Version));
            var hash = await Task.Run(() => ElevatedStep.HashOf(download.Path), cancellationToken);
            var command = new UpdateCommand(download.Path, hash, release.Version);
            var result = await Task.Run(() => ElevatedStep.Run(Environment.ProcessPath!, command, _log), cancellationToken);
            if (result is not (UpdateResult.Installed or UpdateResult.AlreadyActive))
            {
                return result;
            }

            status.Report(SetupText.Get("Configuring"));
            ApplyUserChoices(choices);
            return UpdateResult.Installed;
        }
        finally
        {
            File.Delete(download.Path);
        }
    }

    /// <summary>Starts the installed ZoomIN as the signed-in user.</summary>
    public static void StartZoomIN()
    {
        // Through the shell: CreateProcess refuses UIAccess executables (ADR-0012).
        using var _ = Process.Start(new ProcessStartInfo(ElevatedInstall.Layout.CurrentExecutable) { UseShellExecute = true });
    }

    // Per-user parts run here, not in the elevated step: with "over the shoulder" elevation the
    // administrator may be another account, whose registry and AppData would get the settings.
    private void ApplyUserChoices(SetupChoices choices)
    {
        var exe = ElevatedInstall.Layout.CurrentExecutable;
        new RegistryAutostart(RegistryAutostart.RunKey, $"\"{exe}\"").SetEnabled(choices.Autostart);
        if (choices.DesktopShortcut)
        {
            ShellShortcut.Create(ShellShortcut.UserDesktop, exe);
        }
        else
        {
            ShellShortcut.Remove(ShellShortcut.UserDesktop);
        }

        var store = new JsonSettingsStore(_log);
        store.Save(choices.ApplyTo(store.Load()));
    }
}
