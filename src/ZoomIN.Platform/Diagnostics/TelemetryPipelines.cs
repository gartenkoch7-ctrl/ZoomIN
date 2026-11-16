using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Builds the OpenTelemetry pipelines of one process: logs and operations to the hourly files,
/// plus OTLP when an address is given. Building costs tens of milliseconds (assembly loading, JIT),
/// so the host does it off the start-up path.
/// </summary>
internal sealed class TelemetryPipelines(TelemetryFiles files, string role, TimeProvider time)
{
    private static readonly string s_version =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public ILoggerFactory Logs(Uri? endpoint) => LoggerFactory.Create(builder =>
        builder.SetMinimumLevel(LogLevel.Trace).AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.SetResourceBuilder(Resource());
            options.AddProcessor(new ReplayContextProcessor());
            options.AddProcessor(new BatchLogRecordExportProcessor(
                new LogFileExporter(files, role, time), scheduledDelayMilliseconds: 1000));
            if (endpoint is not null)
            {
                OtlpExport.AddTo(options, endpoint);
            }
        }));

    public TracerProvider Traces(Uri? endpoint)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(Telemetry.Name)
            .SetResourceBuilder(Resource())
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(new BatchActivityExportProcessor(
                new TraceFileExporter(files, role, time), scheduledDelayMilliseconds: 1000));
        if (endpoint is not null)
        {
            OtlpExport.AddTo(builder, endpoint);
        }

        return builder.Build();
    }

    public MeterProvider Metrics(Uri endpoint)
    {
        var builder = Sdk.CreateMeterProviderBuilder().AddMeter(Telemetry.Name).SetResourceBuilder(Resource());
        OtlpExport.AddTo(builder, endpoint);
        return builder.Build();
    }

    /// <summary>Writes operations that ended before the tracer existed (see <see cref="EarlyActivities"/>).</summary>
    public void WriteEarly(Activity[] finished)
    {
        if (finished.Length == 0)
        {
            return;
        }

        using var exporter = new TraceFileExporter(files, role, time);
        exporter.Export(new Batch<Activity>(finished, finished.Length));
    }

    private ResourceBuilder Resource() => ResourceBuilder.CreateEmpty()
        .AddService(Telemetry.Name, serviceVersion: s_version, serviceInstanceId: Environment.ProcessId.ToString(CultureInfo.InvariantCulture))
        .AddAttributes([new(Telemetry.RoleAttribute, role)]);
}
