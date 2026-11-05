namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// Shared state of the filter per window: the window tracker publishes regions, render threads
/// read them lock-free, the settings set the options.
/// </summary>
public sealed class WindowFilterModel
{
    private WindowRegions _regions = WindowRegions.Empty;
    private WindowFilterOptions _options;

    public WindowFilterModel(WindowFilterOptions options)
    {
        _options = options.Normalized();
    }

    /// <summary>The regions changed; renderers wake up. Raised on the tracker thread.</summary>
    public event Action? RegionsChanged;

    /// <summary>The options changed (settings, shortcut); the tracker resolves the windows again.</summary>
    public event Action? OptionsChanged;

    public WindowRegions Regions => Volatile.Read(ref _regions);

    public WindowFilterOptions Options => Volatile.Read(ref _options);

    /// <summary>Takes the new regions; nothing happens (no wake-up) if they look the same.</summary>
    public void Publish(WindowRegions regions)
    {
        if (Regions.ContentEquals(regions))
        {
            return;
        }

        Volatile.Write(ref _regions, regions);
        RegionsChanged?.Invoke();
    }

    public void SetOptions(WindowFilterOptions options)
    {
        Volatile.Write(ref _options, options.Normalized());
        OptionsChanged?.Invoke();
    }
}
