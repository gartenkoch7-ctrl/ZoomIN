using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// The islands at run time (M19 B1). Writers (settings, profile switch, the edit mode, the settings
/// page) run under a lock; render threads read <see cref="Scene"/> lock-free, one immutable object
/// per change (AGENTS.md 2.3). <see cref="Committed"/> is what is saved: the scene may show a drag
/// in progress, which is never written to the settings.
/// </summary>
public sealed class IslandModel
{
    private static readonly UpDownCounter<long> s_count = Telemetry.Meter.CreateUpDownCounter<long>(
        "zoomin.islands.count", unit: "{island}", description: "Islands of the active profile");

    private readonly Lock _gate = new();
    private IslandScene _scene;
    private IslandSet _committed;
    private Func<string, PhysicalRect?>? _boundsOf;

    public IslandModel(IslandSet set, bool visible)
    {
        ArgumentNullException.ThrowIfNull(set);
        _scene = new IslandScene(set, visible, null);
        _committed = set;
        s_count.Add(set.Count);
    }

    /// <summary>Anything drawn changed (also every step of a drag); render threads wake up.</summary>
    public event Action? Changed;

    /// <summary>Islands or their visibility changed in a way that is saved (not raised per drag step).</summary>
    public event Action? Saved;

    public IslandScene Scene => Volatile.Read(ref _scene);

    /// <summary>The islands as they are saved: without a drag that is still running.</summary>
    public IslandSet Committed => Volatile.Read(ref _committed);

    /// <summary>
    /// The islands of a newly applied profile, with new ids (ids are never reused while ZoomIN runs:
    /// still images and the settings rows belong to them). The view of a running edit mode goes at once (it
    /// belongs to the old islands); the edit mode sees the change from outside and ends itself.
    /// </summary>
    public void Replace(IslandSet set, bool visible)
    {
        ArgumentNullException.ThrowIfNull(set);
        IslandSet? saved = null;
        Publish(scene =>
        {
            saved = set.Renumbered(scene.Set.NextId);
            return scene with { Set = Fitted(saved), Visible = visible, Edit = null };
        }, save: true, () => saved);
    }

    /// <summary>
    /// Fits every island to its monitor as it is now, and every profile's islands from now on (the
    /// app passes the monitors at start). Only what is drawn: the saved islands keep their rectangles,
    /// so a smaller monitor for a while (projector) never shrinks them for good.
    /// </summary>
    public void FitTo(Func<string, PhysicalRect?> boundsOf)
    {
        ArgumentNullException.ThrowIfNull(boundsOf);
        Volatile.Write(ref _boundsOf, boundsOf);
        Publish(scene => scene.Edit is null ? scene with { Set = scene.Set.FitTo(boundsOf) } : scene, save: false, () => null);
    }

    public void SetVisible(bool visible) => Publish(scene => scene with { Visible = visible }, save: true, () => null);

    /// <summary>Live or still image for one island (settings page); false if it is gone.</summary>
    public bool SetStill(int id, bool still) =>
        Change(id, (set, index) => set.Replace(index, set[index] with { Still = still, Take = set[index].Take + (still ? 1 : 0) }));

    /// <summary>Removes one island (settings page); false if it is gone.</summary>
    public bool Remove(int id) => Change(id, (set, index) => set.RemoveAt(index));

    /// <summary>
    /// A step of the edit mode (<paramref name="edit"/> = what it draws), but only if the islands are
    /// still <paramref name="expected"/> (what the edit mode published last): a change from outside
    /// (profile switch, settings page) in between is never overwritten. Atomic.
    /// </summary>
    /// <param name="save">The step finished a change (not a drag in progress): saved and committed.</param>
    /// <returns>False if the islands changed from outside; nothing was published.</returns>
    /// <param name="commit">What is saved for a saved step (the edited islands, untouched ones as saved); null = <paramref name="set"/>.</param>
    public bool ShowEdit(IslandSet expected, IslandSet set, IslandEditView edit, bool save, IslandSet? commit = null)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(edit);
        var current = true;

        // Visibility stays the user's: the scene shows every island while editing anyway (IslandScene.ShowsOn).
        Publish(scene =>
        {
            current = ReferenceEquals(scene.Set, expected);
            return current ? scene with { Set = set, Edit = edit } : scene;
        }, save, () => save ? commit ?? set : null);
        return current;
    }

    /// <summary>
    /// Ends the edit mode: its islands <paramref name="edited"/> are kept if the islands are still
    /// <paramref name="expected"/>, otherwise the change from outside stays. Atomic.
    /// </summary>
    /// <param name="changed">The run changed something: saved and committed. Otherwise only drawn
    /// (fitting the islands to the monitors at its start is no change to save).</param>
    /// <param name="commit">What is saved when <paramref name="changed"/>; null = <paramref name="edited"/>.</param>
    public void EndEdit(IslandSet expected, IslandSet edited, bool changed = true, IslandSet? commit = null)
    {
        ArgumentNullException.ThrowIfNull(edited);
        Publish(
            scene => scene with { Set = ReferenceEquals(scene.Set, expected) ? edited : scene.Set, Edit = null },
            save: changed,
            () => changed && ReferenceEquals(Scene.Set, edited) ? commit ?? edited : null);
    }

    // Built on the committed islands, never on a drag in progress (which would be saved with it);
    // a running edit mode loses its view at once and ends without overwriting the change.
    private bool Change(int id, Func<IslandSet, int, IslandSet> change)
    {
        var changed = false;
        IslandSet? saved = null;
        Publish(scene =>
        {
            var index = _committed.IndexOf(id);
            changed = index >= 0;
            saved = changed ? change(_committed, index) : null;
            return saved is not null ? scene with { Set = Fitted(saved), Edit = null } : scene;
        }, save: true, () => saved);
        return changed;
    }

    private IslandSet Fitted(IslandSet set) => Volatile.Read(ref _boundsOf) is { } boundsOf ? set.FitTo(boundsOf) : set;

    // commitAs: what becomes Committed (every caller says it: a drag step must never commit by default), null = keep it.
    private void Publish(Func<IslandScene, IslandScene> change, bool save, Func<IslandSet?> commitAs)
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

            // A drag step shows its islands but does not commit them; a saved change of the edit mode
            // commits what is shown; visibility and fitting commit nothing.
            if (commitAs() is { } committed)
            {
                Volatile.Write(ref _committed, committed);
            }
        }

        s_count.Add(after.Set.Count - before.Set.Count);
        Changed?.Invoke();
        if (save)
        {
            Saved?.Invoke();
        }
    }
}
