using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Settings;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Schemes;

/// <summary>
/// Creates, changes and deletes the own colour schemes (M18, C1–C3). Changes apply live through the
/// effects model and are saved like every setting; deleting one moves everything that chose it –
/// also stored profiles – back to "Inverted". Runs on the UI thread.
/// </summary>
public sealed class CustomSchemeService(RenderModels models, SettingsService settings, IProfileStore profiles, ILogger<CustomSchemeService> log)
{
    private static readonly IReadOnlyList<ColorReplacement> s_noReplacements = [];

    private readonly HashSet<FilterId> _edited = [];

    /// <summary>The schemes as they apply now, sorted by place.</summary>
    public IReadOnlyList<CustomScheme> Schemes => models.Effects.Current.Schemes;

    /// <summary>A new scheme in the first free place, or null with all <see cref="CustomSchemes.Max"/> taken.</summary>
    public CustomScheme? Add(string name)
    {
        using var operation = Telemetry.Source.StartActivity("zoomin.filters.scheme_edit");
        operation?.SetTag("zoomin.operation", "create");

        // The place is picked inside the model's update, from the schemes as they are at that moment.
        CustomScheme? scheme = null;
        models.Effects.UpdateSchemes(current =>
        {
            scheme = CustomSchemes.FreeSlot(current) is { } free
                ? CustomSchemes.NormalizedScheme(new CustomScheme { Id = free, Name = name })
                : null;
            return scheme is null ? current : [.. current, scheme];
        });

        if (scheme is null)
        {
            operation?.SetStatus(ActivityStatusCode.Error, "full");
            log.Full(CustomSchemes.Max);
            return null;
        }

        operation?.SetTag("zoomin.place", scheme.Id.ToString());
        operation?.SetTag("zoomin.kind", scheme.Kind.ToString());
        log.Created(scheme.Id, scheme.Kind);
        return scheme;
    }

    /// <summary>Applies an edited scheme at once (live preview on screen); logged by <see cref="Finish"/>.</summary>
    public void Update(CustomScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        if (CustomSchemes.Find(Schemes, scheme.Id) is null)
        {
            return;
        }

        // An event that changes nothing (slider let go on the same value) publishes nothing.
        models.Effects.UpdateSchemes(current =>
            CustomSchemes.Find(current, scheme.Id) is { } old && SameScheme(old, scheme)
                ? current
                : [.. current.Select(s => s.Id == scheme.Id ? scheme : s)]);
        _edited.Add(scheme.Id);
    }

    /// <summary>The user is done with a scheme (other scheme picked, page left): one log line for all its edits.</summary>
    public void Finish(FilterId id)
    {
        if (!_edited.Remove(id) || CustomSchemes.Find(Schemes, id) is not { } scheme)
        {
            return;
        }

        using var operation = Start("change", id);
        operation?.SetTag("zoomin.kind", scheme.Kind.ToString());
        log.Changed(id, scheme.Kind, scheme.Replacements.Count);
    }

    /// <summary>Shows the scheme now on <paramref name="monitorId"/>: its own filter, or the shared one, like the shortcuts.</summary>
    public void Show(FilterId id, string monitorId)
    {
        using var operation = Start("show", id);
        models.Effects.ShowFilter(monitorId, id);
    }

    /// <summary>Deletes the scheme; returns how many choices fell back to "Inverted".</summary>
    public int Delete(FilterId id)
    {
        if (CustomSchemes.Find(Schemes, id) is not { } scheme)
        {
            return 0;
        }

        using var operation = Start("delete", id);
        _edited.Remove(id);
        var inEffects = models.Effects.RemoveScheme(id);
        var inWindows = 0;
        models.Windows.UpdateOptions(o => SchemeRemoval.Without(o, id, out inWindows));
        var changedProfiles = UpdateStoredProfiles(id, out var inProfiles);

        // The active profile and settings.json get the new state at once, not after the debounce. A
        // locked file must not end the app from a click; the debounced save writes it next time.
        try
        {
            settings.SaveNow();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.SaveFailed(ex, id);
        }

        var fallbacks = inEffects + inWindows + inProfiles;
        operation?.SetTag("zoomin.kind", scheme.Kind.ToString());
        operation?.SetTag("zoomin.fallbacks", fallbacks);
        log.Deleted(id, scheme.Kind);
        if (fallbacks > 0)
        {
            log.FellBack(fallbacks, id, CustomSchemes.Fallback, changedProfiles);
        }

        return fallbacks;
    }

    // Records compare the replacement list by reference; compare its entries instead.
    private static bool SameScheme(CustomScheme a, CustomScheme b) =>
        a with { Replacements = s_noReplacements } == b with { Replacements = s_noReplacements }
        && a.Replacements.SequenceEqual(b.Replacements);

    private static Activity? Start(string operation, FilterId id)
    {
        var activity = Telemetry.Source.StartActivity("zoomin.filters.scheme_edit");
        activity?.SetTag("zoomin.operation", operation);
        activity?.SetTag("zoomin.place", id.ToString());
        return activity;
    }

    // Profiles other than the active one keep their choice on disk; the active one is saved from the models.
    private int UpdateStoredProfiles(FilterId id, out int fallbacks)
    {
        fallbacks = 0;
        var changed = 0;
        var active = settings.Current.ActiveProfile;
        IReadOnlyList<string> names;
        try
        {
            names = profiles.Names();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.ProfileNotUpdated(ex, id);
            return 0;
        }

        foreach (var name in names)
        {
            try
            {
                if (name == active || profiles.Load(name) is not { } profile)
                {
                    continue;
                }

                var updated = SchemeRemoval.Without(profile, id, out var count);
                if (count > 0)
                {
                    profiles.Save(name, updated);
                    fallbacks += count;
                    changed++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.ProfileNotUpdated(ex, id);
            }
        }

        return changed;
    }
}
