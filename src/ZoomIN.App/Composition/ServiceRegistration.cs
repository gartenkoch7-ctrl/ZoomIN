using Microsoft.Extensions.DependencyInjection;
using ZoomIN.App.Views;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Platform.Diagnostics;

namespace ZoomIN.App.Composition;

/// <summary>The only place where concrete Platform/Rendering types are wired to Core interfaces.</summary>
internal static class ServiceRegistration
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILog, FileLog>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
