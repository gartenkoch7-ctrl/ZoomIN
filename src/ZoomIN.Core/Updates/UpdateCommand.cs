using System.Globalization;

namespace ZoomIN.Core.Updates;

/// <summary>
/// Arguments of "ZoomIN.exe --apply-update &lt;package&gt; &lt;sha256&gt; &lt;version&gt;", the elevated
/// step that installs a downloaded package into Program Files (ADR-0015).
/// </summary>
/// <param name="PackagePath">The verified package in the user's download folder.</param>
/// <param name="Sha256">Expected hash; checked again after copying into Program Files.</param>
/// <param name="Version">The version the package contains.</param>
public sealed record UpdateCommand(string PackagePath, string Sha256, AppVersion Version)
{
    public const string Switch = "--apply-update";

    public static bool TryParse(IReadOnlyList<string> args, out UpdateCommand command)
    {
        command = null!;
        if (args.Count != 4 || args[0] != Switch || args[2].Length != 64 ||
            !args[2].All(Uri.IsHexDigit) || !AppVersion.TryParse(args[3], out var version))
        {
            return false;
        }

        command = new UpdateCommand(args[1], args[2].ToLowerInvariant(), version);
        return true;
    }

    /// <summary>Command line for ShellExecute, each argument quoted.</summary>
    public string Format() => string.Create(
        CultureInfo.InvariantCulture, $"{Switch} \"{PackagePath}\" {Sha256} {Version}");
}
