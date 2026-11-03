namespace ZoomIN.Setup.Localization;

/// <summary>English texts of the setup wizard (M9).</summary>
internal static class SetupTextsEnglish
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Title"] = "ZoomIN Setup",
        ["Zoom.In"] = "Larger",
        ["Zoom.Out"] = "Smaller",
        ["Zoom.Hint"] = "Ctrl+Plus/Minus or Ctrl+mouse wheel enlarges this window.",
        ["Back"] = "Back",
        ["Next"] = "Next",
        ["Install"] = "Install",
        ["Finish"] = "Finish",
        ["Retry"] = "Try again",
        ["Progress"] = "Step {0} of {1}",

        ["Step0.Title"] = "Welcome to ZoomIN",
        ["Step0.Text"] = "ZoomIN is a free screen magnifier. This setup installs ZoomIN for all users of this PC. Windows asks for administrator rights once.",
        ["Language"] = "Language",

        ["Step1.Title"] = "Choose a version",
        ["Step1.Text"] = "Stable contains finished versions. Experimental also contains release candidates and develop builds; these are untested.",
        ["Channel"] = "Channel",
        ["Stable"] = "Stable",
        ["Experimental"] = "Experimental",
        ["Versions"] = "Available versions",
        ["PreRelease"] = "pre-release",
        ["Loading"] = "Looking for versions …",
        ["None"] = "No versions found. Please check the internet connection.",
        ["Found"] = "{0} versions found. The newest one is selected.",
        ["Refresh"] = "Search again",

        ["Step2.Title"] = "Settings",
        ["Step2.Text"] = "Everything can be changed later in ZoomIN.",
        ["Autostart"] = "Start ZoomIN with Windows",
        ["Desktop"] = "Shortcut on the desktop",
        ["Updates"] = "Check for new versions",
        ["SecureDesktop"] = "Also magnify the sign-in screen, lock screen and administrator prompts",
        ["Updates.Desc"] = "At start-up and once a day. Nothing is installed without a click.",
        ["ZoomLevel"] = "Zoom level",
        ["Filter"] = "Colour filter",
        ["Filter.Off"] = "Off",
        ["Filter.Inverted"] = "Invert colours",
        ["Filter.Grayscale"] = "Grayscale",
        ["Filter.YellowOnBlack"] = "Yellow on black",
        ["Filter.BlackOnYellow"] = "Black on yellow",
        ["Filter.WhiteOnBlue"] = "White on blue",

        ["Step3.Title"] = "Installation",
        ["Legacy"] = "Removing the old ZoomIN installation …",
        ["Downloading"] = "Downloading {0} …",
        ["Installing"] = "Installing {0} … Please confirm the Windows prompt.",
        ["Configuring"] = "Applying the settings …",
        ["Result.PackageMismatch"] = "The downloaded package is damaged or altered and was discarded.",
        ["Result.SignatureRejected"] = "The package is not signed by ZoomIN and was not installed.",
        ["Result.Declined"] = "Cancelled: the Windows prompt was declined. Nothing was changed.",
        ["Result.Failed"] = "Installing failed. Details are in %LocalAppData%\\ZoomIN\\logs.",

        ["Step4.Title"] = "Done",
        ["Step4.Text"] = "ZoomIN {0} is installed. Zoom on/off: Ctrl+Alt+Z. Larger and smaller: Ctrl+Alt+Page up/down or Ctrl+mouse wheel.",
        ["StartNow"] = "Start ZoomIN now",
    };
}
