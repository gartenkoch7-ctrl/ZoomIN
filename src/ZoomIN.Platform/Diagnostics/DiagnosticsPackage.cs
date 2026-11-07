using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// The ZIP a user attaches to a bug report (ADR-0019): the kept log and operations files plus a
/// short info file (version, Windows, recording level). Nothing else – no settings, no paths
/// beyond file names, no window contents. Files still being written by other ZoomIN processes are
/// read with shared access. The last <see cref="Kept"/> packages are kept, older ones removed.
/// </summary>
public static class DiagnosticsPackage
{
    public const int Kept = 3;

    private const string Prefix = "ZoomIN-Diagnose-";

    /// <summary>Writes the package into <paramref name="folder"/> and returns its path.</summary>
    public static string Create(TelemetryHost telemetry, TelemetryOptions options, string folder)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        ArgumentNullException.ThrowIfNull(options);
        using var operation = Telemetry.Source.StartActivity("zoomin.diagnostics.package");

        // Everything still buffered belongs in the package.
        telemetry.Flush(TimeSpan.FromSeconds(2));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{Prefix}{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var files = telemetry.KeptFiles();
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var file in files)
            {
                AddShared(zip, file);
            }

            var info = zip.CreateEntry("ZoomIN-Info.txt");
            using var writer = new StreamWriter(info.Open(), new UTF8Encoding(false));
            writer.Write(Info(telemetry, options, files.Count));
        }

        operation?.SetTag("zoomin.diagnostics.files", files.Count);
        RemoveOld(folder);
        return path;
    }

    private static void AddShared(ZipArchive zip, string file)
    {
        try
        {
            using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var target = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.Optimal).Open();
            source.CopyTo(target);
        }
        catch (IOException)
        {
            // Removed by the clean-up in the meantime: nothing to add.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string Info(TelemetryHost telemetry, TelemetryOptions options, int files)
    {
        var effective = options.Effective(DateTimeOffset.Now);
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"ZoomIN {Version()}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Windows: {Environment.OSVersion.Version} ({RuntimeInformation.OSArchitecture})");
        text.AppendLine(CultureInfo.InvariantCulture, $".NET: {RuntimeInformation.FrameworkDescription}");
        text.AppendLine(CultureInfo.InvariantCulture, $"UI language: {CultureInfo.CurrentUICulture.Name}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Role: {telemetry.Role}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Recording: {effective.Level}, logs from {effective.Filter.MinLogLevel}, operations {effective.Filter.Traces}, metrics {effective.Filter.Metrics}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Kept: {effective.RetentionDays} day(s), {files} file(s)");

        // Whether one is set, not the address: it may name an internal server.
        text.AppendLine(CultureInfo.InvariantCulture, $"Export address set: {effective.OtlpEndpoint.Length > 0}");
        text.AppendLine("Secure desktop logs are in the SYSTEM profile and not included.");
        return text.ToString();
    }

    private static string Version() =>
        System.Reflection.Assembly.GetEntryAssembly()?
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "unknown";

    private static void RemoveOld(string folder)
    {
        foreach (var old in Directory.EnumerateFiles(folder, Prefix + "*.zip").Order(StringComparer.Ordinal).SkipLast(Kept))
        {
            try
            {
                File.Delete(old);
            }
            catch (IOException)
            {
                // Open in another program: removed next time.
            }
        }
    }
}
