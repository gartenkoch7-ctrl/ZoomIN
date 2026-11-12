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

    /// <summary>Replaces the options outright; prefer <see cref="UpdateOptions"/> for changes based on the current ones.</summary>
    public void SetOptions(WindowFilterOptions options) => UpdateOptions(_ => options);

    /// <summary>
    /// Changes the options from their current state in one step, so a shortcut on another thread and
    /// a change on the settings page cannot overwrite each other (M18: deleting an own scheme).
    /// A change that returns the current options as they are does nothing: no event, no save.
    /// </summary>
    public void UpdateOptions(Func<WindowFilterOptions, WindowFilterOptions> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        WindowFilterOptions current, next;
        do
        {
            current = Volatile.Read(ref _options);
            var changed = change(current);
            if (ReferenceEquals(changed, current))
            {
                return;
            }

            next = changed.Normalized();
        }
        while (Interlocked.CompareExchange(ref _options, next, current) != current);

        OptionsChanged?.Invoke();
    }
}
