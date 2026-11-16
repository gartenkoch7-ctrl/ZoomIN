using System.Diagnostics;

namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// Hands the current operation to a ZoomIN process started by another one, so an update is one
/// trace across setup, updater, elevated helper and app (ADR-0019). Passed on the command line,
/// because an elevated start ("runas") does not reliably keep environment variables; the W3C
/// variable <see cref="Variable"/> is accepted as well.
/// </summary>
public static class TraceParentArgument
{
    public const string Switch = "--traceparent";

    public const string Variable = "TRACEPARENT";

    /// <summary>Removes "--traceparent &lt;id&gt;" so the remaining arguments parse as before.</summary>
    public static string[] Extract(string[] args, out string? traceParent)
    {
        traceParent = null;
        var index = Array.IndexOf(args, Switch);
        if (index < 0)
        {
            return args;
        }

        if (index + 1 < args.Length)
        {
            traceParent = args[index + 1];
        }

        var rest = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (i != index && i != index + 1)
            {
                rest.Add(args[i]);
            }
        }

        return [.. rest];
    }

    /// <summary>
    /// <paramref name="arguments"/> plus the current operation, if there is one in W3C format.
    /// </summary>
    public static string Append(string arguments)
    {
        if (Activity.Current is not { IdFormat: ActivityIdFormat.W3C, Id: { } id })
        {
            return arguments;
        }

        return arguments.Length == 0 ? $"{Switch} {id}" : $"{arguments} {Switch} {id}";
    }
}
