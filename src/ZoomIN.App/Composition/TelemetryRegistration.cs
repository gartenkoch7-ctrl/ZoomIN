using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZoomIN.Platform.Diagnostics;

namespace ZoomIN.App.Composition;

/// <summary>Makes the process's telemetry available to every service as <see cref="ILogger{TCategoryName}"/>.</summary>
internal static class TelemetryRegistration
{
    public static IServiceCollection AddTelemetry(this IServiceCollection services, TelemetryHost telemetry)
    {
        // Registered as instances, so the container does not dispose them: the host does, after
        // the services are gone and have logged their last lines.
        services.AddSingleton(telemetry);
        services.AddSingleton(telemetry.Loggers);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        return services;
    }
}
