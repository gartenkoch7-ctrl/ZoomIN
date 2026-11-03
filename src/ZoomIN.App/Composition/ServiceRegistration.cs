using Microsoft.Extensions.DependencyInjection;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Updates;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.SecureDesktop;
using ZoomIN.Platform.Settings;
using ZoomIN.Platform.Shell;
using ZoomIN.Platform.Updates;

namespace ZoomIN.App.Composition;

/// <summary>
/// The only place where concrete Platform/Rendering types are wired to Core interfaces; the
/// shared engine part is in <see cref="EngineRegistration"/>.
/// </summary>
internal static class ServiceRegistration
{
    /// <summary>The tray app on the user's desktop.</summary>
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILog, FileLog>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IProfileStore, JsonProfileStore>();
        services.AddEngine();

        services.AddSingleton<ITrayIcon, Win32TrayIcon>();
        services.AddSingleton<IAutostart, RegistryAutostart>();
        services.AddSingleton<IReleaseFeed, GitHubReleaseFeed>();
        services.AddSingleton<IPackageDownloader, HttpPackageDownloader>();
        services.AddSingleton<ISettingsBackup, FolderSettingsBackup>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<AppShell>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// The secure-desktop instance (ADR-0016): engine only, settings read-only from the session
    /// user, nothing that opens windows, files chosen by the user or the network.
    /// </summary>
    public static ServiceProvider BuildSecureDesktop()
    {
        var userFolder = SessionUserFolders.SessionUserZoomFolder();
        var services = new ServiceCollection();
        services.AddSingleton<ILog, FileLog>();
        services.AddSingleton<ISettingsStore>(sp =>
            new SecureSettingsStore(sp.GetRequiredService<ILog>(), userFolder, SessionUserFolders.MachineFolder));
        services.AddSingleton<IProfileStore>(new SecureProfileStore(userFolder));
        services.AddEngine();
        return services.BuildServiceProvider();
    }
}
