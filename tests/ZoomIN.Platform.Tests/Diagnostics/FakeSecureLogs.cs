using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Tests.Diagnostics;

/// <summary>Stands in for the elevated copy: never asks for administrator rights.</summary>
internal sealed class FakeSecureLogs(SecureLogsResult result) : ISecureLogsRequest
{
    public string? Target { get; private set; }

    public SecureLogsResult Copy(string target)
    {
        Target = target;
        if (result == SecureLogsResult.Copied)
        {
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "logs-zoomin.log"), "secure line");
            File.WriteAllText(Path.Combine(target, "debug-shot.png"), "not a log");
        }

        return result;
    }
}
