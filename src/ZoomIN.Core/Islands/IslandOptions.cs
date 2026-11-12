namespace ZoomIN.Core.Islands;

/// <summary>
/// The islands as saved per profile (M19 B1). Properties use <c>set</c> for the JSON source
/// generator (see the remark on AppSettings); treat instances as immutable.
/// </summary>
public sealed record IslandOptions
{
    public static IslandOptions Default { get; } = new();

    /// <summary>Ctrl+Alt+I: islands shown (factory) or hidden.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>At most <see cref="IslandRules.MaxCount"/>; more are ignored when loading.</summary>
    public IReadOnlyList<IslandEntry> Items { get; set; } = [];
}
