namespace ZoomIN.App.Localization;

/// <summary>English texts of mouse buffer and axis lock (A6, page "Pointer").</summary>
internal static class TextsEnglishPointerConstraint
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Constraint.Title"] = "Limit movement",
        ["Constraint.Buffer"] = "Mouse buffer at monitor edges",
        ["Constraint.Buffer.Desc"] = "The pointer stops at the edge of a monitor and does not slip onto the next one by accident. It only crosses after you push against the edge for a moment. Applies between monitors only, not at the edge of the lens.",
        ["Constraint.Buffer.Off"] = "Off",
        ["Constraint.Buffer.Light"] = "Light (short push, 0.2 seconds)",
        ["Constraint.Buffer.Medium"] = "Medium (push for 0.4 seconds)",
        ["Constraint.Buffer.Strong"] = "Strong (long push, 0.8 seconds)",
        ["Constraint.Axis"] = "Axis lock",
        ["Constraint.Axis.Desc"] = "While you hold the hold key, the pointer moves only horizontally or only vertically, for example along a table row. The first movement decides the direction. Release the key and the pointer is free again at once.",
        ["Constraint.Key"] = "Hold key for the axis lock",
        ["Constraint.Key.Desc"] = "Note: Shift and dragging selects text in many programs, and releasing Alt often opens the menu. Choose a key you rarely hold on its own.",
        ["Constraint.Key.LeftShift"] = "Left Shift",
        ["Constraint.Key.RightShift"] = "Right Shift",
        ["Constraint.Key.LeftControl"] = "Left Ctrl",
        ["Constraint.Key.RightControl"] = "Right Ctrl",
        ["Constraint.Key.LeftAlt"] = "Left Alt",
        ["Constraint.Key.RightAlt"] = "Alt Gr (right Alt)",
    };
}
