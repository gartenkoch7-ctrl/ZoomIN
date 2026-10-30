using System.Reflection;

namespace ZoomIN.Architecture.Tests;

/// <summary>Structural conventions from AGENTS.md 2.2 and 5.</summary>
public sealed class ConventionTests
{
    private static readonly string[] s_forbiddenNamespaceSegments = ["Services", "Helpers", "Utils", "Utilities"];

    [Fact]
    public void Namespaces_AreOrganizedByFeature()
    {
        var offenders = OwnTypes()
            .Where(type => (type.Namespace ?? string.Empty)
                .Split('.')
                .Intersect(s_forbiddenNamespaceSegments)
                .Any())
            .Select(type => type.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, "Organize by feature, not by type kind: " + string.Join(", ", offenders));
    }

    [Fact]
    public void PInvoke_OnlyInInteropNamespaces()
    {
        var offenders = OwnTypes()
            .Where(type => !IsInteropNamespace(type.Namespace))
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.Attributes.HasFlag(MethodAttributes.PinvokeImpl))
            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .ToList();

        Assert.True(offenders.Count == 0, "P/Invoke outside *.Interop: " + string.Join(", ", offenders));
    }

    private static IEnumerable<Type> OwnTypes() =>
        Layers.All
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.StartsWith("ZoomIN", StringComparison.Ordinal) == true);

    // CsWin32 generates its P/Invoke surface into Windows.Win32, which OwnTypes() already skips.
    private static bool IsInteropNamespace(string? ns) =>
        ns is not null && (ns.EndsWith(".Interop", StringComparison.Ordinal) || ns.Contains(".Interop.", StringComparison.Ordinal));
}
