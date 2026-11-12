using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Islands;

/// <summary>Maps between the persisted <see cref="AppSettings"/> and the runtime <see cref="IslandModel"/> (M19 B1).</summary>
public static class IslandMapping
{
    public static IslandModel Create(AppSettings settings) => new(Set(settings), Options(settings).Visible);

    /// <summary>The options of the settings; a missing or null entry (older files) gives the defaults.</summary>
    public static IslandOptions Options(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Islands ?? IslandOptions.Default;
    }

    /// <summary>The islands of the settings, at most four, without null entries. Fitted to monitors later, when they are known.</summary>
    public static IslandSet Set(AppSettings settings) =>
        IslandSet.From((Options(settings).Items ?? []).Where(entry => entry is not null).Select(entry => entry.ToIsland()));

    public static AppSettings Apply(AppSettings settings, IslandModel model)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(model);
        var scene = model.Scene;
        return settings with
        {
            Islands = new IslandOptions { Visible = scene.Visible, Items = [.. scene.Set.Items.Select(IslandEntry.From)] },
        };
    }

    /// <summary>Applies a profile's islands to the running model (profile switch).</summary>
    public static void ApplyTo(IslandModel model, AppSettings profile)
    {
        ArgumentNullException.ThrowIfNull(model);
        model.Replace(Set(profile), Options(profile).Visible);
    }
}
