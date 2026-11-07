using Microsoft.Extensions.DependencyInjection;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.SecureDesktop;
using ZoomIN.Platform.Shell;
using ZoomIN.Platform.Updates;

namespace ZoomIN.App.Composition;

/// <summary>Installation services shared by the tray app (updates) and the short-lived roles (ADR-0025).</summary>
internal static class InstallRegistration
{
    public static IServiceCollection AddInstall(this IServiceCollection services)
    {
        services.AddSingleton<IShortcuts, WindowsShortcuts>();
        services.AddSingleton<IInstallRegistration, WindowsInstallRegistration>();
        services.AddSingleton<ISecureDesktopInstall, SystemSecureDesktopInstall>();
        services.AddSingleton<IElevatedStep, ShellElevatedStep>();
        services.AddSingleton<ISignatureVerifier, AuthenticodeVerifier>();
        services.AddSingleton<IMessageDialog, Win32MessageDialog>();
        return services;
    }
}
