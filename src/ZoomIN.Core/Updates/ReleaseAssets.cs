namespace ZoomIN.Core.Updates;

/// <summary>
/// Asset names every release must carry so the setup and the app can install it (ADR-0014).
/// The release workflows produce exactly these names.
/// </summary>
public static class ReleaseAssets
{
    public const string ChecksumsName = "checksums.txt";

    /// <summary>The setup program; installs the UIAccess build (signed on the PC, ADR-0031) where ZoomIN is not installed yet.</summary>
    public const string SetupName = "ZoomIN-Setup.exe";

    /// <summary>The zipped UIAccess build (unsigned, ADR-0031) of <paramref name="version"/>.</summary>
    public static string PackageName(AppVersion version) => $"ZoomIN-{version}-win-x64-uiaccess.zip";
}
