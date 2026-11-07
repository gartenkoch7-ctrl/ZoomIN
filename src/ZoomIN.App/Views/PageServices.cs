using Microsoft.Extensions.DependencyInjection;

namespace ZoomIN.App.Views;

/// <summary>
/// The navigation parameter of every settings page (M14 step 10). WinUI's Frame creates pages
/// without DI, so this is their one link to the container: view models are built with exactly the
/// services their constructors name, instead of one record holding everything (former PageContext).
/// </summary>
public sealed class PageServices(IServiceProvider services)
{
    /// <summary>A new view model (or other UI helper) with its dependencies from the container.</summary>
    public T Create<T>()
        where T : class => ActivatorUtilities.CreateInstance<T>(services);

    /// <summary>A registered service a page uses directly (pointer images, window handle).</summary>
    public T Get<T>()
        where T : notnull => services.GetRequiredService<T>();
}
