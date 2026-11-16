using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// The ZIP a user attaches to a bug report (ADR-0019): the kept log and operations files plus a
/// short info file (version, Windows, recording level). Nothing else – no settings, no paths
/// beyond file names, no window contents. Files still being written by other ZoomIN processes are
/// read with shared access. The last <see cref="Kept"/> packages are kept, older ones removed. On
/// request the logs of the secure instance are added (M10, Q7): an elevated step copies them out of
/// SYSTEM's profile first, into "secure/" in the ZIP.
/// </summary>
/// <param name="secureLogs">The elevated copy; null where it is not offered (the option is then hidden).</param>
public sealed class DiagnosticsPackage(TelemetryHost telemetry, IAppFolders folders, ISecureLogsRequest? secureLogs = null) : IDiagnosticsPackage
{
    public const int Kept = 3;

    private const string Prefix = "ZoomIN-Diagnose-";
    private const string SecureEntryFolder = "secure/";

    public bool CanIncludeSecureLogs => secureLogs is not null;

    public DiagnosticsPackageResult Create(TelemetryOptions options, bool includeSecureLogs = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        var folder = folders.Diagnostics();
        using var operation = Telemetry.Source.StartActivity("zoomin.diagnostics.package");

        Directory.CreateDirectory(folder);
        var secureFolder = includeSecureLogs && secureLogs is not null ? Path.Combine(folder, SecureLogCopyRule.NewFolderName()) : null;
        SecureLogsResult? secure = null;
        string path;
        try
        {
            // First, so its log line (2340, the exit code) is flushed into the package below.
            secure = secureFolder is null ? null : secureLogs!.Copy(secureFolder);

            // Everything still buffered belongs in the package.
            telemetry.Flush(TimeSpan.FromSeconds(2));
            path = Path.Combine(folder, $"{Prefix}{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            var files = telemetry.KeptFiles();
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    _ = AddShared(zip, file, Path.GetFileName(file));
                }

                var secureFiles = secure == SecureLogsResult.Copied ? AddSecure(zip, secureFolder!) : 0;
                var info = zip.CreateEntry("ZoomIN-Info.txt");
                using var writer = new StreamWriter(info.Open(), new UTF8Encoding(false));
                writer.Write(Info(telemetry, options, files.Count, secure, secureFiles));
            }

            operation?.SetTag("zoomin.diagnostics.files", files.Count);
            operation?.SetTag("zoomin.diagnostics.secure_logs", secure?.ToString().ToLowerInvariant() ?? "not_requested");
        }
        finally
        {
            RemoveCopy(secureFolder);
        }

        RemoveOld(folder);
        return new DiagnosticsPackageResult(path, secure);
    }

    private static int AddSecure(ZipArchive zip, string folder)
    {
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        var added = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder).Where(f => SecureLogCopyRule.IsCopiedFile(Path.GetFileName(f))))
            {
                if (AddShared(zip, file, SecureEntryFolder + Path.GetFileName(file)))
                {
                    added++;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The copy went away or cannot be listed: the package keeps what was added so far.
        }

        return added;
    }

    /// <returns>False if the file could not be read (and is not in the ZIP).</returns>
    private static bool AddShared(ZipArchive zip, string file, string entryName)
    {
        try
        {
            using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var target = zip.CreateEntry(entryName, CompressionLevel.Optimal).Open();
            source.CopyTo(target);
            return true;
        }
        catch (IOException)
        {
            // Removed by the clean-up in the meantime: nothing to add.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Info(TelemetryHost telemetry, TelemetryOptions options, int files, SecureLogsResult? secure, int secureFiles)
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
        text.AppendLine(secure switch
        {
            null => "Secure desktop logs are in the SYSTEM profile and not included.",
            SecureLogsResult.Copied => string.Create(CultureInfo.InvariantCulture, $"Secure desktop logs: included, {secureFiles} file(s) in {SecureEntryFolder}"),
            SecureLogsResult.Declined => "Secure desktop logs: not included, the administrator prompt was declined.",
            _ => "Secure desktop logs: not included, copying them failed (exit code in the log of role app, event 2342).",
        });
        return text.ToString();
    }

    private static string Version() =>
        System.Reflection.Assembly.GetEntryAssembly()?
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "unknown";

    // The copy is in the ZIP now; it holds logs of the sign-in screen, so it does not stay around.
    private static void RemoveCopy(string? folder)
    {
        try
        {
            if (folder is not null && Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Open in another program: it is only logs, removed by hand or never seen again.
        }
    }

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

        // Copies a slow elevated step finished after the package gave up on it (still held then).
        foreach (var stale in Directory.EnumerateDirectories(folder, SecureLogCopyRule.FolderPrefix + "*"))
        {
            RemoveCopy(stale);
        }
    }
}
