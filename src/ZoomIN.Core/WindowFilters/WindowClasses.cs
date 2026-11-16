namespace ZoomIN.Core.WindowFilters;

/// <summary>Decides the <see cref="WindowRole"/> from what Windows reports about a top-level window.</summary>
public static class WindowClasses
{
    // Classes that are always popups of another window. Dialogs (#32770) are measured on their own.
    private static readonly HashSet<string> s_popups = new(StringComparer.OrdinalIgnoreCase)
    {
        "#32768", "tooltips_class32", "ComboLBox", "Xaml_WindowedPopupClass", "SysDragImage", "DropDown",
        "Net UI Tool Window", "Net UI Tool Window Layered", "MsoCommandBarPopup",
    };

    // Drawn by Windows next to other windows and see-through: they must not cover anything.
    private static readonly HashSet<string> s_ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "SysShadow", "Ghost",
    };

    /// <summary>Windows that never take part: shadows and the like.</summary>
    public static bool IsIgnored(string windowClass) => s_ignored.Contains(windowClass);

    /// <param name="kind">App + class.</param>
    /// <param name="isPopupStyle">WS_POPUP without a caption (no title bar).</param>
    /// <param name="hasOwner">The window has an owner window (GW_OWNER).</param>
    /// <param name="isToolWindow">WS_EX_TOOLWINDOW: not shown in the taskbar.</param>
    public static WindowRole RoleOf(WindowKind kind, bool isPopupStyle, bool hasOwner, bool isToolWindow)
    {
        if (WindowFilterPresets.IsShell(kind))
        {
            return WindowRole.Shell;
        }

        if (s_popups.Contains(kind.WindowClass))
        {
            return WindowRole.Popup;
        }

        // Menus of Chromium, Office, WinUI and others are captionless popups owned by the app
        // window, or captionless tool windows. Splash screens (no owner, in the taskbar) are measured.
        return isPopupStyle && (hasOwner || isToolWindow) ? WindowRole.Popup : WindowRole.Normal;
    }
}
