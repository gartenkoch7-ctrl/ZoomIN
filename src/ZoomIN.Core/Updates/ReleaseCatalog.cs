namespace ZoomIN.Core.Updates;

/// <summary>Picks the releases a channel offers and classifies a version change (ADR-0014).</summary>
public static class ReleaseCatalog
{
    /// <summary>Releases of <paramref name="channel"/>, newest first.</summary>
    public static IReadOnlyList<ReleaseInfo> Offered(IEnumerable<ReleaseInfo> releases, ReleaseChannel channel) =>
        releases
            .Where(release => channel == ReleaseChannel.Experimental || !release.Version.IsPreRelease)
            .OrderByDescending(release => release.Version)
            .ToList();

    /// <summary>The newest release of <paramref name="channel"/> above <paramref name="current"/>, or null.</summary>
    public static ReleaseInfo? Update(IEnumerable<ReleaseInfo> releases, ReleaseChannel channel, AppVersion current) =>
        Offered(releases, channel).FirstOrDefault(release => release.Version > current);

    /// <summary>The newest release of <paramref name="channel"/> that carries a setup program, or null.</summary>
    public static ReleaseInfo? NewestSetup(IEnumerable<ReleaseInfo> releases, ReleaseChannel channel) =>
        Offered(releases, channel).FirstOrDefault(release => release.SetupUrl is not null);

    public static UpdateKind Classify(AppVersion current, AppVersion target) => target.CompareTo(current) switch
    {
        > 0 => UpdateKind.Upgrade,
        < 0 => UpdateKind.Downgrade,
        _ => UpdateKind.Reinstall,
    };
}
