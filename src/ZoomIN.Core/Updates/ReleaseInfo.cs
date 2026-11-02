namespace ZoomIN.Core.Updates;

/// <summary>One installable release from the feed, with the two assets the setup needs.</summary>
/// <param name="Version">Parsed from the release tag.</param>
/// <param name="PublishedAt">When the release was published.</param>
/// <param name="PackageUrl">The signed UIAccess package (<see cref="ReleaseAssets.PackageName"/>).</param>
/// <param name="ChecksumsUrl">SHA-256 list (<see cref="ReleaseAssets.ChecksumsName"/>) to verify the package.</param>
public sealed record ReleaseInfo(AppVersion Version, DateTimeOffset PublishedAt, Uri PackageUrl, Uri ChecksumsUrl);
