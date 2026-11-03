using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.Settings;
using ZoomIN.Platform.Shell;
using ZoomIN.Platform.Updates;

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

    /// <param name="steps">Ticked step by step; the caller marks the running step failed on an error result.</param>
    public async Task<UpdateResult> InstallAsync(
        ReleaseInfo release, SetupChoices choices, InstallChecklist steps, IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(steps);
        steps.Begin(InstallStep.RemoveOld);
        if (await Task.Run(() => InstallRegistration.RemoveLegacyInstall(TimeSpan.FromSeconds(60)), cancellationToken))
        {
            _log.Info("[Setup] Old Inno installation removed.");
        }

        steps.Begin(InstallStep.Download);
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
            steps.Begin(InstallStep.Install);
            var hash = await Task.Run(() => ElevatedStep.HashOf(download.Path), cancellationToken);
            var command = new UpdateCommand(download.Path, hash, release.Version, choices.SecureDesktop);
            var result = await Task.Run(() => ElevatedStep.Run(Environment.ProcessPath!, command, _log), cancellationToken);
            if (result is not (UpdateResult.Installed or UpdateResult.AlreadyActive))
            {
                return result;
            }

            steps.Begin(InstallStep.Configure);
            ApplyUserChoices(choices);
            steps.Finish(succeeded: true);
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
