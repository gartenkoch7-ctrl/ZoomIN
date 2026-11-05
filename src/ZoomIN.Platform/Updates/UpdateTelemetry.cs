using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Operations of check, download and installation, all tagged the same way (ADR-0019): the
/// version and the result as a word (e.g. "SignatureRejected"), never paths. Together with
/// "--traceparent" an update is one trace from the click in the app to the new version's start.
/// </summary>
public static class UpdateTelemetry
{
    public const string VersionTag = "zoomin.version";
    public const string ResultTag = "zoomin.update.result";

    public static Activity? Start(string name, AppVersion? version = null)
    {
        var operation = Telemetry.Source.StartActivity(name);
        if (operation is not null && version is not null)
        {
            operation.SetTag(VersionTag, version.ToString());
        }

        return operation;
    }

    /// <summary>Records <paramref name="result"/>; a result that is not <paramref name="ok"/> marks the operation failed.</summary>
    public static T Finish<T>(this Activity? operation, T result, bool ok)
        where T : struct, Enum
    {
        if (operation is not null)
        {
            operation.SetTag(ResultTag, result.ToString());
            if (!ok)
            {
                operation.SetStatus(ActivityStatusCode.Error, result.ToString());
            }
        }

        return result;
    }

    public static bool Succeeded(UpdateResult result) => result is UpdateResult.Installed or UpdateResult.AlreadyActive;
}
