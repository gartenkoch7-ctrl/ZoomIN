using System.Diagnostics;
using System.Text;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Runs Windows PowerShell's Set-AuthenticodeSignature for <see cref="LocalSigner"/> (ADR-0031): the
/// only signing API that ships with every Windows without the SDK (signtool) or mssign32 interop.
/// Runs elevated with a clean environment, so nothing the signed-in user controls loads into it.
/// </summary>
internal sealed class SigningProcess : ISigningProcess
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(60);

    // PowerShell treats these like ' inside single-quoted strings; each is escaped by doubling it.
    private static readonly char[] s_quotes = ['\'', '‘', '’', '‚', '‛'];

    /// <returns>0 if the signature is valid, otherwise the exit code (-1 after a timeout).</returns>
    public int Run(string executablePath, string thumbprint)
    {
        var script = $$"""
            Import-Module "$PSHOME\Modules\Microsoft.PowerShell.Security"
            $c = Get-Item -LiteralPath 'Cert:\LocalMachine\My\{{Quote(thumbprint)}}'
            $s = Microsoft.PowerShell.Security\Set-AuthenticodeSignature -LiteralPath '{{Quote(executablePath)}}' -Certificate $c -HashAlgorithm SHA256
            if ($s.Status -ne 'Valid') { exit 2 }
            """;
        var powershell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0");
        var start = new ProcessStartInfo(Path.Combine(powershell, "powershell.exe")) { CreateNoWindow = true, UseShellExecute = false };

        // The user's environment (HKCU\Environment) could load code into this elevated process while
        // the signing key exists: PSModulePath to a planted module, COR_PROFILER to a profiler DLL. A
        // clean environment with only what Windows PowerShell needs.
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var temp = Path.Combine(windows, "Temp");
        start.Environment.Clear();
        start.Environment["SystemRoot"] = windows;
        start.Environment["windir"] = windows;
        start.Environment["PATH"] = Environment.SystemDirectory + ";" + powershell;
        start.Environment["TEMP"] = temp;
        start.Environment["TMP"] = temp;
        start.Environment["PSModulePath"] = Path.Combine(powershell, "Modules");
        foreach (var argument in (string[])["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(script))])
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new IOException("PowerShell did not start.");
        if (process.WaitForExit(s_timeout))
        {
            return process.ExitCode;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or AggregateException or System.ComponentModel.Win32Exception)
        {
            // Exited between the timeout and the kill, or a child could not be ended: the signature
            // is not valid either way, and the caller withdraws the trust.
        }

        return -1;
    }

    /// <summary>A value for a single-quoted PowerShell string; internal for tests.</summary>
    internal static string Quote(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            text.Append(c);
            if (Array.IndexOf(s_quotes, c) >= 0)
            {
                text.Append(c);
            }
        }

        return text.ToString();
    }
}
