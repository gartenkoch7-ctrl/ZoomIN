namespace ZoomIN.Core.SecureDesktop;

/// <summary>
/// The Ease of Access "Configuration" value: a comma-separated list of assistive technologies that
/// Windows starts on the sign-in screen (ADR-0016). Other entries (e.g. Windows Magnifier's
/// "magnifierpane") must survive our changes unchanged.
/// </summary>
public static class AtConfigurationList
{
    public static string Add(string? list, string name) =>
        Contains(list, name) ? list! : string.Join(',', Items(list).Append(name));

    public static string Remove(string? list, string name) =>
        string.Join(',', Items(list).Where(item => !item.Equals(name, StringComparison.OrdinalIgnoreCase)));

    public static bool Contains(string? list, string name) =>
        Items(list).Any(item => item.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string[] Items(string? list) =>
        (list ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
