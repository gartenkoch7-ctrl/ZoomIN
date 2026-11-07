using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.SecureDesktop;
using ZoomIN.App.Localization;
using ZoomIN.App.Updates;
using ZoomIN.App.Views;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Localization;
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
    public static ServiceProvider Build(TelemetryHost telemetry, IAppFolders folders) =>
        Tray(telemetry, folders).BuildServiceProvider();

    /// <summary>The tray app's registrations; the container tests validate them without starting it.</summary>
    internal static ServiceCollection Tray(TelemetryHost telemetry, IAppFolders folders)
    {
        var services = new ServiceCollection();
        services.AddTelemetry(telemetry);
        services.AddSingleton(folders);

        // One localizer for the app; XAML reaches it through the static facade Text (ADR-0025).
        var localizer = AppTexts.CreateLocalizer();
        Text.Use(localizer);
        services.AddSingleton<ILocalizer>(localizer);

        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IProfileStore, JsonProfileStore>();
        services.AddEngine();

        services.AddSingleton<ITrayIcon, Win32TrayIcon>();
        services.AddSingleton<IAutostart, RegistryAutostart>();
        services.AddSingleton<HttpClient, UpdateHttpClient>();
        services.AddSingleton<IReleaseFeed, GitHubReleaseFeed>();
        services.AddSingleton<IPackageDownloader, HttpPackageDownloader>();
        services.AddSingleton<ISettingsBackup, FolderSettingsBackup>();
        services.AddInstall();
        services.AddSingleton<IUiAccessProbe, Win32UiAccessProbe>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<IDiagnosticsPackage, DiagnosticsPackage>();
        services.AddSingleton<SetupLauncher>();
        services.AddSingleton<ISecureDesktopSwitch, SecureDesktopSwitch>();
        services.AddSingleton<IWatchdogLauncher, ShellWatchdogLauncher>();
        services.AddSingleton<AppShell>();
        services.AddSingleton<IShellActions>(sp => sp.GetRequiredService<AppShell>());
        services.AddSingleton<PageServices>();
        return services;
    }

    /// <summary>
    /// A short-lived role without UI (update step, uninstall): installation services and the
    /// role's flow only, no engine. Built per process in <see cref="Program"/> (M14 step 7).
    /// </summary>
    public static ServiceProvider BuildRole(TelemetryHost telemetry, IAppFolders folders) =>
        Role(telemetry, folders).BuildServiceProvider();

    /// <summary>The short-lived role's registrations.</summary>
    internal static ServiceCollection Role(TelemetryHost telemetry, IAppFolders folders)
    {
        var services = new ServiceCollection();
        services.AddTelemetry(telemetry);
        services.AddSingleton(folders);
        services.AddSingleton<ILocalizer>(AppTexts.CreateLocalizer());
        services.AddInstall();
        services.AddSingleton<UpdateRunner>();
        services.AddSingleton<UninstallFlow>();
        services.AddSingleton<ISecureDesktopSwitch, SecureDesktopSwitch>();
        return services;
    }

    /// <summary>
    /// The secure-desktop instance (ADR-0016): engine only, settings read-only from the session
    /// user, nothing that opens windows, files chosen by the user or the network.
    /// </summary>
    public static ServiceProvider BuildSecureDesktop(TelemetryHost telemetry, IAppFolders folders) =>
        SecureDesktop(telemetry, folders).BuildServiceProvider();

    /// <summary>The secure-desktop instance's registrations.</summary>
    internal static ServiceCollection SecureDesktop(TelemetryHost telemetry, IAppFolders folders)
    {
        var services = new ServiceCollection();
        services.AddTelemetry(telemetry);
        services.AddSingleton(folders);

        // Created by a factory, so the provider disposes it (and the user's token) at exit.
        services.AddSingleton(sp => SessionUser.Open(sp.GetRequiredService<ILogger<SessionUser>>(), folders));
        services.AddSingleton<ISettingsStore>(sp => new SecureSettingsStore(
            sp.GetRequiredService<ILogger<SecureSettingsStore>>(), sp.GetRequiredService<SessionUser>(), folders.Local));
        services.AddSingleton<IProfileStore>(sp => new SecureProfileStore(sp.GetRequiredService<SessionUser>()));
        services.AddSingleton<IWatchdogLauncher, ShellWatchdogLauncher>();
        services.AddEngine();
        return services;
    }
}
