using System.Globalization;

namespace ZoomIN.Core.Updates;

/// <summary>
/// Arguments of "ZoomIN.exe --apply-update &lt;package&gt; &lt;sha256&gt; &lt;version&gt; [--secure-desktop on|off]",
/// the elevated step that installs a downloaded package into Program Files (ADR-0015).
/// </summary>
/// <param name="PackagePath">The verified package in the user's download folder.</param>
/// <param name="Sha256">Expected hash; checked again after copying into Program Files.</param>
/// <param name="Version">The version the package contains.</param>
/// <param name="SecureDesktop">
/// The setup's choice for the sign-in screen (ADR-0016); null keeps the machine's preference (updates).
/// </param>
public sealed record UpdateCommand(string PackagePath, string Sha256, AppVersion Version, bool? SecureDesktop = null)
{
    public const string Switch = "--apply-update";
    public const string SecureDesktopFlag = "--secure-desktop";

    public static bool TryParse(IReadOnlyList<string> args, out UpdateCommand command)
    {
        command = null!;
        if (args.Count is not (4 or 6) || args[0] != Switch || args[2].Length != 64 ||
            !args[2].All(Uri.IsHexDigit) || !AppVersion.TryParse(args[3], out var version))
        {
            return false;
        }

        bool? secureDesktop = null;
        if (args.Count == 6)
        {
            if (args[4] != SecureDesktopFlag || args[5] is not ("on" or "off"))
            {
                return false;
            }

            secureDesktop = args[5] == "on";
        }

        command = new UpdateCommand(args[1], args[2].ToLowerInvariant(), version, secureDesktop);
        return true;
    }

    /// <summary>Command line for ShellExecute, each argument quoted.</summary>
    public string Format()
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"{Switch} \"{PackagePath}\" {Sha256} {Version}");
        return SecureDesktop is { } on ? $"{line} {SecureDesktopFlag} {(on ? "on" : "off")}" : line;
    }
}
