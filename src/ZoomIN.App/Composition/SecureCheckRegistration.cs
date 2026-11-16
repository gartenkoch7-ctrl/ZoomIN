using Microsoft.Extensions.DependencyInjection;
using ZoomIN.App.SecureDesktop.Checks;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.App.Composition;

/// <summary>Start timings, checks and the snapshot shortcut of the secure instance (M10 completion).</summary>
internal static class SecureCheckRegistration
{
    /// <summary>Adds to <see cref="ServiceRegistration.SecureDesktop"/>; the tray app never has them.</summary>
    public static IServiceCollection AddSecureChecks(this IServiceCollection services)
    {
        services.AddSingleton<ISecureDesktopProbe, Win32SecureDesktopProbe>();
        services.AddSingleton<SecureDesktopChecks>();
        services.AddSingleton<SecureSnapshotShortcut>();
        services.AddSingleton<SecureStartWatch>();
        return services;
    }
}
