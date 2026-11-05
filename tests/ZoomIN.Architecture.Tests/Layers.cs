using System.Reflection;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Watchdog;

namespace ZoomIN.Architecture.Tests;

/// <summary>Assemblies under test, located via one known type each.</summary>
internal static class Layers
{
    public static Assembly Core { get; } = typeof(Telemetry).Assembly;

    public static Assembly Platform { get; } = typeof(TelemetryHost).Assembly;

    public static Assembly Rendering { get; } = typeof(GpuSupport).Assembly;

    public static Assembly Watchdog { get; } = typeof(WatchdogRunner).Assembly;

    public static IEnumerable<Assembly> All => [Core, Platform, Rendering, Watchdog];
}
