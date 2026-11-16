using System.Text;

namespace ZoomIN.Core.Lifecycle;

/// <summary>
/// Builds a Windows command line that CommandLineToArgvW (and .NET's Main) splits back into the
/// same arguments. Needed where ZoomIN starts itself through CreateProcess, which takes one string.
/// </summary>
public static class CommandLine
{
    public static string Join(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var line = new StringBuilder();
        foreach (var arg in args)
        {
            if (line.Length > 0)
            {
                line.Append(' ');
            }

            Append(line, arg);
        }

        return line.ToString();
    }

    private static void Append(StringBuilder line, string arg)
    {
        if (arg.Length > 0 && !arg.Any(c => c is ' ' or '\t' or '\n' or '\v' or '"'))
        {
            line.Append(arg);
            return;
        }

        // Backslashes are literal unless they precede a quote: then each one needs doubling.
        line.Append('"');
        var backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            line.Append('\\', c == '"' ? (backslashes * 2) + 1 : backslashes);
            line.Append(c);
            backslashes = 0;
        }

        line.Append('\\', backslashes * 2);
        line.Append('"');
    }
}
