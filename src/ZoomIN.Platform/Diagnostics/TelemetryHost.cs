using ZoomIN.Core.Folders;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Starts telemetry the same way for every ZoomIN process (ADR-0019): logs and operations go to
/// hourly files, an OTLP export only with an address and never from the secure desktop. Starting
/// costs only a few milliseconds: the OpenTelemetry pipelines are built in the background, and
/// what happens until then is kept and written afterwards. <see cref="Apply"/> switches to the
/// user's setting once it is loaded and again every hour, which also ends a raised setting after
/// seven days and cleans up old files.
/// </summary>
public sealed class TelemetryHost : IDisposable
{
    private const string EndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private static readonly TimeSpan Recheck = TimeSpan.FromHours(1);
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(5);

    private readonly TelemetryRole _role;
    private readonly string _roleName;
    private readonly TelemetryFiles _files;
    private readonly TelemetryPipelines _pipelines;
    private readonly TimeProvider _time;
    private readonly ActivityContext _parent;
    private readonly SwitchableLoggerProvider _logs = new();
    private readonly EarlyActivities _early;
    private readonly Task _ready;
    private readonly ITimer _timer;
    private readonly object _gate = new();
    private TelemetryOptions _chosen = TelemetryOptions.Default;
    private (bool Traces, bool Metrics, Uri? Endpoint)? _built;
    private TracerProvider? _tracer;
    private MeterProvider? _meter;

    private TelemetryHost(TelemetryRole role, string? traceParent, string folder, TimeProvider time)
    {
        _role = role;
        _roleName = role.ToString().ToLowerInvariant();
        _time = time;
        _files = new TelemetryFiles(folder, time);
        _pipelines = new TelemetryPipelines(_files, _roleName, time);
        ActivityContext.TryParse(traceParent ?? Environment.GetEnvironmentVariable(TraceParentArgument.Variable), null, out _parent);
        Loggers = new HostLoggerFactory(_logs);
        _early = new EarlyActivities(() => _logs.Filter.Traces);
        _ready = Task.Run(() => Apply(TelemetryOptions.Default, waitUntilReady: false));
        // Not the public Apply: its "nothing changed" shortcut would skip the fallback and clean-up.
        _timer = time.CreateTimer(_ => Apply(_chosen, waitUntilReady: true), null, Recheck, Recheck);
    }

    public ILoggerFactory Loggers { get; }

    /// <summary>Log and operations folder of this process (diagnostics package).</summary>
    public string Folder => _files.Folder;

    public TelemetryRole Role => _role;

    /// <summary>The kept log and operations files, oldest first (diagnostics package).</summary>
    public IReadOnlyList<string> KeptFiles() => _files.KeptFiles();

    /// <param name="folders">Logs go to its Local folder; on the secure desktop SYSTEM's profile (ADR-0016).</param>
    public static TelemetryHost Start(TelemetryRole role, IAppFolders folders, string? traceParent = null) =>
        new(role, traceParent, folders.Logs(), TimeProvider.System);

    /// <summary>For tests: own folder and clock.</summary>
    internal static TelemetryHost Start(TelemetryRole role, string? traceParent, string folder, TimeProvider time) =>
        new(role, traceParent, folder, time);

    public ILogger<T> Logger<T>() => new Logger<T>(Loggers);

    /// <summary>
    /// Starts the operation <paramref name="name"/> as child of the one that started this process,
    /// if any; null while operations are not recorded.
    /// </summary>
    public Activity? StartOperation(string name) =>
        Telemetry.Source.StartActivity(name, ActivityKind.Internal, _parent)?.SetTag(Telemetry.RoleAttribute, _roleName);

    /// <summary>
    /// Switches to <paramref name="options"/> (as stored); applies the seven-day fallback. Returns
    /// at once if nothing changed – the app calls this on every settings change, zoom included.
    /// </summary>
    public void Apply(TelemetryOptions options)
    {
        if (options == Volatile.Read(ref _chosen))
        {
            return;
        }

        Apply(options, waitUntilReady: true);
    }

    /// <summary>Writes everything buffered, e.g. before the process ends after a crash.</summary>
    public void Flush(TimeSpan timeout)
    {
        _ready.Wait(timeout);
        var ms = (int)timeout.TotalMilliseconds;
        lock (_gate)
        {
            _tracer?.ForceFlush(ms);
            _meter?.ForceFlush(ms);
            _logs.Replace(_pipelines.Logs(_built?.Endpoint));
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _ready.Wait(ReadyTimeout);
        lock (_gate)
        {
            _early.Dispose();
            _tracer?.Dispose();
            _meter?.Dispose();
        }

        _logs.Dispose();
    }

    private void Apply(TelemetryOptions options, bool waitUntilReady)
    {
        if (waitUntilReady)
        {
            _ready.Wait(ReadyTimeout);
        }

        lock (_gate)
        {
            _chosen = options;
            var effective = options.Effective(_time.GetLocalNow());
            var filter = effective.Filter;
            var endpoint = Endpoint(effective);
            _files.RetentionDays = effective.RetentionDays;
            _logs.Filter = filter;

            var wanted = (filter.Traces, filter.Metrics && endpoint is not null, endpoint);
            if (wanted != _built)
            {
                Rebuild(wanted);
            }
        }

        _files.CleanUp();
    }

    private void Rebuild((bool Traces, bool Metrics, Uri? Endpoint) wanted)
    {
        var first = _built is null;
        if (first || wanted.Endpoint != _built!.Value.Endpoint)
        {
            _logs.Replace(_pipelines.Logs(wanted.Endpoint));
        }

        _tracer?.Dispose();
        _tracer = wanted.Traces ? _pipelines.Traces(wanted.Endpoint) : null;
        _meter?.Dispose();
        _meter = wanted.Metrics && wanted.Endpoint is { } metricsEndpoint ? _pipelines.Metrics(metricsEndpoint) : null;
        _built = wanted;

        if (first)
        {
            // From now on the tracer records; what ended before is written once, by hand.
            var finished = _early.HandOver();
            if (wanted.Traces)
            {
                _pipelines.WriteEarly(finished);
            }
        }
    }

    // SYSTEM on the sign-in screen never sends anything; elsewhere the setting wins over OTEL_*.
    private Uri? Endpoint(TelemetryOptions options)
    {
        if (_role == TelemetryRole.Secure)
        {
            return null;
        }

        var address = options.OtlpEndpoint.Length > 0 ? options.OtlpEndpoint : Environment.GetEnvironmentVariable(EndpointVariable);
        return Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;
    }
}
