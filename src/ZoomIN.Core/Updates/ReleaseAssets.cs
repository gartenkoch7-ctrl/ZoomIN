namespace ZoomIN.Core.Updates;

/// <summary>
/// Asset names every release must carry so the setup and the app can install it (ADR-0014).
/// The release workflows produce exactly these names.
/// </summary>
public static class ReleaseAssets
{
    public const string ChecksumsName = "checksums.txt";

    /// <summary>The setup program; installs the signed UIAccess build where ZoomIN is not installed yet.</summary>
    public const string SetupName = "ZoomIN-Setup.exe";

    /// <summary>The zipped, signed UIAccess build of <paramref name="version"/>.</summary>
    public static string PackageName(AppVersion version) => $"ZoomIN-{version}-win-x64-uiaccess.zip";
}
