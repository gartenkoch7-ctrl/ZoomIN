namespace ZoomIN.App.Updates;

/// <summary>Outcome of <see cref="SetupLauncher.LaunchAsync"/>.</summary>
public enum SetupLaunch
{
    Started,

    /// <summary>No release of the user's channel carries a setup program yet.</summary>
    NoRelease,

    /// <summary>No connection, or the file did not match the release's checksum.</summary>
    DownloadFailed,

    /// <summary>The setup is not signed by ZoomIN in a way Windows trusts; it was not started.</summary>
    NotTrusted,
}
