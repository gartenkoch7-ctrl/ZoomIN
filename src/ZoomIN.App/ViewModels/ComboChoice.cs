namespace ZoomIN.App.ViewModels;

/// <summary>Reads an enum choice from a ComboBox index (show conditions, hold key, mouse follow; M17/M18).</summary>
internal static class ComboChoice
{
    /// <summary>
    /// The value at <paramref name="index"/>, or <paramref name="current"/> for −1 (a ComboBox reports
    /// no selection while its items are replaced or it unloads: not a choice) and for unknown indexes.
    /// </summary>
    public static T Or<T>(int index, T current)
        where T : struct, Enum
    {
        if (index < 0)
        {
            return current;
        }

        var value = (T)Enum.ToObject(typeof(T), index);
        return Enum.IsDefined(value) ? value : current;
    }
}
