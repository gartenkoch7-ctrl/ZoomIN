using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// The only code that touches the OTLP exporter. Kept in its own class so its assembly is loaded
/// only when an export address is set (start-up budget, ADR-0019).
/// </summary>
internal static class OtlpExport
{
    // The usual collector ports: 4317 speaks gRPC, 4318 HTTP (e.g. Aspire Dashboard, Jaeger).
    private const int HttpPort = 4318;

    public static void AddTo(OpenTelemetryLoggerOptions options, Uri endpoint) =>
        options.AddOtlpExporter(o => Configure(o, endpoint, "/v1/logs"));

    public static void AddTo(TracerProviderBuilder builder, Uri endpoint) =>
        builder.AddOtlpExporter(o => Configure(o, endpoint, "/v1/traces"));

    public static void AddTo(MeterProviderBuilder builder, Uri endpoint) =>
        builder.AddOtlpExporter(o => Configure(o, endpoint, "/v1/metrics"));

    private static void Configure(OtlpExporterOptions options, Uri endpoint, string signalPath)
    {
        if (endpoint.Port == HttpPort)
        {
            options.Protocol = OtlpExportProtocol.HttpProtobuf;

            // Set in code (not by OTEL_* variables), the HTTP exporter does not add the signal path.
            options.Endpoint = endpoint.AbsolutePath is "/" or ""
                ? new Uri(endpoint, signalPath)
                : endpoint;
        }
        else
        {
            options.Protocol = OtlpExportProtocol.Grpc;
            options.Endpoint = endpoint;
        }
    }
}
