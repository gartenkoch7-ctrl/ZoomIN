using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Islands;

/// <summary>
/// The islands at run time (M19 B1). Writers (settings, profile switch, the edit mode, the settings
/// page) run under a lock; render threads read <see cref="Scene"/> lock-free, one immutable object
/// per change (AGENTS.md 2.3).
/// </summary>
public sealed class IslandModel
{
    private static readonly UpDownCounter<long> s_count = Telemetry.Meter.CreateUpDownCounter<long>(
        "zoomin.islands.count", unit: "{island}", description: "Islands of the active profile");

    private readonly Lock _gate = new();
    private IslandScene _scene;

    public IslandModel(IslandSet set, bool visible)
    {
        ArgumentNullException.ThrowIfNull(set);
        _scene = new IslandScene(set, visible, null);
        s_count.Add(set.Count);
    }

    /// <summary>Anything drawn changed (also every step of a drag); render threads wake up.</summary>
    public event Action? Changed;

    /// <summary>Islands or their visibility changed in a way that is saved (not raised per drag step).</summary>
    public event Action? Saved;

    public IslandScene Scene => Volatile.Read(ref _scene);

    /// <summary>The islands of a newly applied profile; ends nothing, an edit mode keeps its view.</summary>
    public void Replace(IslandSet set, bool visible)
    {
        ArgumentNullException.ThrowIfNull(set);
        Publish(scene => scene with { Set = set, Visible = visible }, save: true);
    }

    public void SetVisible(bool visible) => Publish(scene => scene with { Visible = visible }, save: true);

    /// <summary>Live or still image for one island (settings page); false if it is gone.</summary>
    public bool SetStill(int id, bool still) => Change(id, (set, index) => set.Replace(index, set[index] with { Still = still }));

    /// <summary>Removes one island (settings page); false if it is gone.</summary>
    public bool Remove(int id) => Change(id, (set, index) => set.RemoveAt(index));

    /// <summary>A step of the edit mode: islands and what the mode draws; <paramref name="edit"/> null ends it.</summary>
    /// <param name="save">The step finished a change (not a drag in progress).</param>
    public void ShowEdit(IslandSet set, IslandEditView? edit, bool save)
    {
        ArgumentNullException.ThrowIfNull(set);
        // Visibility stays the user's: the scene shows every island while editing anyway (IslandScene.ShowsOn).
        Publish(scene => scene with { Set = set, Edit = edit }, save);
    }

    private bool Change(int id, Func<IslandSet, int, IslandSet> change)
    {
        var changed = false;
        Publish(scene =>
        {
            var index = scene.Set.IndexOf(id);
            if (index < 0)
            {
                return scene;
            }

            changed = true;
            return scene with { Set = change(scene.Set, index) };
        }, save: true);
        return changed;
    }

    private void Publish(Func<IslandScene, IslandScene> change, bool save)
    {
        IslandScene before;
        IslandScene after;
        lock (_gate)
        {
            before = _scene;
            after = change(before);
            if (after == before)
            {
                return;
            }

            Volatile.Write(ref _scene, after);
        }

        s_count.Add(after.Set.Count - before.Set.Count);
        Changed?.Invoke();
        if (save)
        {
            Saved?.Invoke();
        }
    }
}
