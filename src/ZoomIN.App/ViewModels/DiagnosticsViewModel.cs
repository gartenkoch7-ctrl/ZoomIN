using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Localization;
using ZoomIN.App.Settings;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Settings;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Shell;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// The diagnostics section of the general page (M11, ADR-0019): recording level, retention,
/// export address and the diagnostics package. Every change is saved at once and reaches the
/// telemetry host through the settings' Changed event.
/// </summary>
public sealed partial class DiagnosticsViewModel : ObservableObject
{
    // Order of the log level list on the page.
    private static readonly LogLevel[] s_logLevels = [LogLevel.Error, LogLevel.Warning, LogLevel.Information, LogLevel.Debug];

    private readonly SettingsService _settings;
    private readonly bool _ready;

    public DiagnosticsViewModel(SettingsService settings)
    {
        _settings = settings;
        var options = settings.Current.Telemetry().Effective(DateTimeOffset.Now);
        LevelIndex = (int)options.Level;
        Logs = options.Logs;
        LogLevelIndex = Math.Max(Array.IndexOf(s_logLevels, options.LogLevel), 0);
        Traces = options.Traces;
        Metrics = options.Metrics;
        RetentionIndex = options.RetentionDays - 1;
        Endpoint = options.OtlpEndpoint;
        Refresh(options);
        _ready = true;
    }

    public IReadOnlyList<string> Levels { get; } =
        [Text.Get("Diag.ErrorsOnly"), Text.Get("Diag.Normal"), Text.Get("Diag.Verbose"), Text.Get("Diag.Custom")];

    public IReadOnlyList<string> LogLevels { get; } =
        [Text.Get("Diag.FromError"), Text.Get("Diag.FromWarning"), Text.Get("Diag.FromInfo"), Text.Get("Diag.FromDebug")];

    public IReadOnlyList<string> RetentionChoices { get; } =
        [.. Enumerable.Range(1, TelemetryOptions.MaxRetentionDays).Select(days => days == 1 ? Text.Get("Diag.Day") : Text.Format("Diag.Days", days))];

    [ObservableProperty]
    public partial int LevelIndex { get; set; }

    [ObservableProperty]
    public partial bool Logs { get; set; }

    [ObservableProperty]
    public partial int LogLevelIndex { get; set; }

    [ObservableProperty]
    public partial bool Traces { get; set; }

    [ObservableProperty]
    public partial bool Metrics { get; set; }

    [ObservableProperty]
    public partial int RetentionIndex { get; set; }

    [ObservableProperty]
    public partial string Endpoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCustom { get; set; }

    [ObservableProperty]
    public partial bool IsRaised { get; set; }

    [ObservableProperty]
    public partial string RaisedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EndpointWarning { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasEndpointWarning { get; set; }

    [ObservableProperty]
    public partial string PackageStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanCreatePackage { get; set; } = true;

    /// <summary>"Reset now": back to the default level and one day.</summary>
    public void ResetRaised()
    {
        LevelIndex = (int)TelemetryLevel.Normal;
        RetentionIndex = 0;
    }

    public async void CreatePackage()
    {
        if (Program.Telemetry is not { } telemetry)
        {
            return;
        }

        CanCreatePackage = false;
        PackageStatus = Text.Get("Diag.Creating");
        var options = _settings.Current.Telemetry();
        try
        {
            var path = await Task.Run(() => DiagnosticsPackage.Create(telemetry, options, DiagnosticsPackage.DefaultFolder));
            PackageStatus = Text.Format("Diag.Created", Path.GetFileName(path));
            FileReveal.Reveal(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PackageStatus = Text.Get("Diag.CreateFailed");
        }
        finally
        {
            CanCreatePackage = true;
        }
    }

    partial void OnLevelIndexChanged(int value) => Save();

    partial void OnLogsChanged(bool value) => Save();

    partial void OnLogLevelIndexChanged(int value) => Save();

    partial void OnTracesChanged(bool value) => Save();

    partial void OnMetricsChanged(bool value) => Save();

    partial void OnRetentionIndexChanged(int value) => Save();

    partial void OnEndpointChanged(string value) => Save();

    private void Save()
    {
        IsCustom = LevelIndex == (int)TelemetryLevel.Custom;
        if (!_ready || LevelIndex < 0 || RetentionIndex < 0 || LogLevelIndex < 0)
        {
            return;
        }

        var endpoint = Endpoint.Trim();
        var valid = TelemetryEndpoint.IsValid(endpoint);
        var chosen = new TelemetryOptions(
            (TelemetryLevel)LevelIndex, Logs, Traces, Metrics, s_logLevels[LogLevelIndex], RetentionIndex + 1, null,
            valid ? endpoint : _settings.Current.TelemetryOtlpEndpoint);
        var options = TelemetryOptions.Chosen(chosen, DateTimeOffset.Now);
        _settings.Update(s => s.WithTelemetry(options));
        Refresh(options, valid);
    }

    private void Refresh(TelemetryOptions options, bool endpointValid = true)
    {
        IsCustom = options.Level == TelemetryLevel.Custom;
        IsRaised = options.RaisedUntil is not null;
        RaisedText = options.RaisedUntil is { } until
            ? Text.Format("Diag.Raised.Desc", until.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))
            : string.Empty;
        EndpointWarning = !endpointValid ? Text.Get("Diag.Export.Invalid")
            : Endpoint.Trim().Length > 0 && !TelemetryEndpoint.IsLocal(Endpoint) ? Text.Get("Diag.Export.Remote")
            : string.Empty;
        HasEndpointWarning = EndpointWarning.Length > 0;
    }
}
