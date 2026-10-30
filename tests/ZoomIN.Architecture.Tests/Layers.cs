using System.Reflection;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Architecture.Tests;

/// <summary>Assemblies under test, located via one known type each.</summary>
internal static class Layers
{
    public static Assembly Core { get; } = typeof(ILog).Assembly;

    public static Assembly Platform { get; } = typeof(FileLog).Assembly;

    public static Assembly Rendering { get; } = typeof(GpuSupport).Assembly;

    public static IEnumerable<Assembly> All => [Core, Platform, Rendering];
}
