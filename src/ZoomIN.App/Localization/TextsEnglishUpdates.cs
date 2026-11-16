namespace ZoomIN.App.Localization;

/// <summary>English texts of the updates page (M9).</summary>
internal static class TextsEnglishUpdates
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["NavUpdates"] = "Updates",
        ["Updates.Installed"] = "Installed version",
        ["Updates.Channel"] = "Channel",
        ["Updates.Channel.Desc"] = "Experimental includes release candidates and develop builds. They are untested and may contain bugs.",
        ["Updates.Stable"] = "Stable",
        ["Updates.Experimental"] = "Experimental",
        ["Updates.Versions"] = "Available versions",
        ["Updates.Refresh"] = "Check now",
        ["Updates.PreRelease"] = "pre-release",
        ["Updates.Upgrade"] = "Update to {0}",
        ["Updates.Downgrade"] = "Go back to {0}",
        ["Updates.Current"] = "{0} is installed",
        ["Updates.DowngradeHint"] = "Settings and profiles are backed up before going back.",
        ["Updates.NotInstalled"] = "This ZoomIN was not installed with the setup (developer build or portable ZIP). Updates from the app are only available for an installed ZoomIN.",
        ["Updates.Loading"] = "Looking for versions …",
        ["Updates.None"] = "No versions found. Without internet or before the first release the list is empty.",
        ["Updates.UpToDate"] = "ZoomIN is up to date.",
        ["Updates.Available"] = "Version {0} is available.",
        ["Updates.Downloading"] = "Downloading {0} …",
        ["Updates.Installing"] = "Installing {0} … Please confirm the Windows prompt.",
        ["Updates.Result.Installed"] = "{0} is installed. ZoomIN restarts …",
        ["Updates.Result.Declined"] = "Cancelled: the Windows prompt was declined. Nothing was changed.",
        ["Updates.Result.PackageMismatch"] = "The downloaded package is damaged or altered and was discarded.",
        ["Updates.Result.SignatureRejected"] = "The package is not signed by ZoomIN and was not installed.",
        ["Updates.Result.AlreadyActive"] = "This version is already installed.",
        ["Updates.Result.Failed"] = "Installing failed. Details are in the log (Info & status).",

        ["Uninstall.Title"] = "Uninstall ZoomIN",
        ["Uninstall.Question"] = "ZoomIN will be removed from this PC.\n\nKeep your settings and profiles?\n\nYes: keep settings\nNo: delete everything\nCancel: change nothing",
        ["Uninstall.Done"] = "ZoomIN has been removed.",
        ["Uninstall.Failed"] = "ZoomIN was not removed. The Windows prompt was declined or the removal failed.",

        ["UiAccess.Title"] = "Zoom is not above all menus",
        ["UiAccess.Tray"] = "The taskbar, sound, network and Start menus stay visible unmagnified. Open ZoomIN to install the signed version.",
        ["UiAccess.Banner"] = "This ZoomIN may not draw above the taskbar, quick settings (sound, network, Bluetooth), Start and the notification center – they stay visible unmagnified. Only the signed, installed version can.",
        ["UiAccess.Install"] = "Install signed version",
        ["UiAccess.Downloading"] = "Downloading and checking the setup …",
        ["UiAccess.Result.Started"] = "The setup has started. Please confirm the Windows prompt.",
        ["UiAccess.Result.NoRelease"] = "There is no version with a setup in your update channel yet.",
        ["UiAccess.Result.DownloadFailed"] = "The setup could not be downloaded. Please check the internet connection.",
        ["UiAccess.Result.NotTrusted"] = "The available setup is not publicly signed yet and was not started. This button works once the signature is in place.",
    };
}
