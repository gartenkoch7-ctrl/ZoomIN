using System.Reflection;
using NetArchTest.Rules;

namespace ZoomIN.Architecture.Tests;

/// <summary>AGENTS.md 2.2: dependencies only point towards Core.</summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void Core_HasNoPlatformGpuOrUiDependencies()
    {
        var result = Types.InAssembly(Layers.Core)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ZoomIN.Platform",
                "ZoomIN.Rendering",
                "ZoomIN.App",
                "Windows",
                "Vortice",
                "Microsoft.UI",
                "Microsoft.Windows")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Core_ReferencesNoOtherZoomInAssembly()
    {
        AssertNoZoomInReferences(Layers.Core);
    }

    [Fact]
    public void Platform_ReferencesOnlyCore()
    {
        AssertNoZoomInReferences(Layers.Platform, allowed: "ZoomIN.Core");
    }

    [Fact]
    public void Rendering_ReferencesOnlyCore()
    {
        AssertNoZoomInReferences(Layers.Rendering, allowed: "ZoomIN.Core");
    }

    [Fact]
    public void Platform_DoesNotDependOnRenderingOrApp()
    {
        var result = Types.InAssembly(Layers.Platform)
            .ShouldNot()
            .HaveDependencyOnAny("ZoomIN.Rendering", "ZoomIN.App", "Vortice")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Rendering_DoesNotDependOnPlatformOrApp()
    {
        var result = Types.InAssembly(Layers.Rendering)
            .ShouldNot()
            .HaveDependencyOnAny("ZoomIN.Platform", "ZoomIN.App", "Microsoft.UI")
            .GetResult();

        AssertSuccessful(result);
    }

    private static void AssertNoZoomInReferences(Assembly assembly, params string[] allowed)
    {
        var forbidden = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("ZoomIN", StringComparison.Ordinal))
            .Except(allowed)
            .ToList();

        Assert.True(
            forbidden.Count == 0,
            $"{assembly.GetName().Name} must not reference: {string.Join(", ", forbidden)}");
    }

    private static void AssertSuccessful(NetArchTest.Rules.TestResult result)
    {
        var offenders = result.FailingTypeNames ?? [];
        Assert.True(result.IsSuccessful, "Layer violation in: " + string.Join(", ", offenders));
    }
}
