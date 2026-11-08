using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ZoomIN.App.Composition;
using ZoomIN.App.Views;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Platform.Diagnostics;

namespace ZoomIN.App.Tests.Composition;

/// <summary>
/// Settings pages build their view models from the container (PageServices, M14 step 10). A missing
/// registration shows only when the page opens, so this checks every constructor parameter of
/// every view model up front, and the whole graph of each role, without creating any service or window.
/// </summary>
public sealed class ContainerTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ZoomIN-AppTests-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void EveryViewModel_HasAllItsServicesRegistered()
    {
        var folders = new TestFolders(_folder);
        using var telemetry = TelemetryHost.Start(TelemetryRole.App, folders);
        using var services = ServiceRegistration.Build(telemetry, folders);
        var known = services.GetRequiredService<IServiceProviderIsService>();

        var viewModels = typeof(PageServices).Assembly.GetTypes()
            .Where(t => t.Namespace == "ZoomIN.App.ViewModels" && t.Name.EndsWith("ViewModel", StringComparison.Ordinal) && !t.IsAbstract)
            .ToList();
        var missing = viewModels
            .SelectMany(vm => SingleConstructor(vm).GetParameters().Select(p => (vm, p.ParameterType)))
            .Where(entry => !known.IsService(entry.ParameterType))
            .Select(entry => $"{entry.vm.Name}({entry.ParameterType.Name})")
            .ToList();

        Assert.True(viewModels.Count >= 14, $"Found only {viewModels.Count} view models.");
        Assert.True(missing.Count == 0, "Not registered: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("tray")]
    [InlineData("role")]
    [InlineData("secure")]
    public void EveryRole_ResolvesItsWholeGraph(string role)
    {
        // ValidateOnBuild walks every registration's constructor chain without creating anything,
        // so a missing service fails here instead of at the first resolve in the field.
        var folders = new TestFolders(_folder);
        using var telemetry = TelemetryHost.Start(TelemetryRole.App, folders);
        var services = role switch
        {
            "tray" => ServiceRegistration.Tray(telemetry, folders),
            "role" => ServiceRegistration.Role(telemetry, folders),
            _ => ServiceRegistration.SecureDesktop(telemetry, folders),
        };

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider);
    }

    private static ConstructorInfo SingleConstructor(Type type)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.True(constructors.Length == 1, $"{type.Name} needs exactly one public constructor for ActivatorUtilities.");
        return constructors[0];
    }

    private sealed record TestFolders(string Root) : IAppFolders
    {
        public string Local => Root;

        public string Roaming => Path.Combine(Root, "roaming");
    }
}
