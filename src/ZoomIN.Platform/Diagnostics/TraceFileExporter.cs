using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Writes finished operations (spans) as one JSON object per line to the hourly operations file.
/// Attributes that could hold user content are dropped (<see cref="TelemetryPrivacy"/>), even if
/// a caller set them by mistake.
/// </summary>
internal sealed class TraceFileExporter : BaseExporter<Activity>
{
    private readonly TelemetryFiles _files;
    private readonly string _role;
    private readonly TimeProvider _time;

    public TraceFileExporter(TelemetryFiles files, string role, TimeProvider time)
    {
        _files = files;
        _role = role;
        _time = time;
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        var lines = new StringBuilder();
        var failed = false;
        using var buffer = new MemoryStream();
        foreach (var activity in batch)
        {
            buffer.SetLength(0);
            using (var json = new Utf8JsonWriter(buffer))
            {
                Write(json, activity);
            }

            lines.Append(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length)).AppendLine();
            failed |= activity.Status == ActivityStatusCode.Error;
        }

        // A failed operation is what a bug report needs most: it passes the daily limit.
        _files.Append(_files.TraceFile(_time.GetLocalNow()), lines.ToString(), essential: failed);
        return ExportResult.Success;
    }

    private void Write(Utf8JsonWriter json, Activity activity)
    {
        json.WriteStartObject();
        json.WriteString("time", activity.StartTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        json.WriteString("name", activity.DisplayName);
        json.WriteNumber("ms", Math.Round(activity.Duration.TotalMilliseconds, 2));
        json.WriteString("status", activity.Status.ToString());
        if (!string.IsNullOrEmpty(activity.StatusDescription))
        {
            json.WriteString("error", activity.StatusDescription);
        }

        json.WriteString("role", _role);
        json.WriteNumber("pid", Environment.ProcessId);
        json.WriteString("trace", activity.TraceId.ToHexString());
        json.WriteString("span", activity.SpanId.ToHexString());
        if (activity.ParentSpanId != default)
        {
            json.WriteString("parent", activity.ParentSpanId.ToHexString());
        }

        json.WriteStartObject("tags");
        foreach (var (key, value) in activity.TagObjects)
        {
            if (TelemetryPrivacy.IsAllowedAttribute(key))
            {
                json.WriteString(key, Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }

        json.WriteEndObject();
        json.WriteEndObject();
    }
}
