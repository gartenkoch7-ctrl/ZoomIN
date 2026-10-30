namespace ZoomIN.Core.Lifecycle;

/// <summary>Opt-in check for a newer release (M7 decision). Never downloads or installs anything.</summary>
public interface IUpdateChecker
{
    /// <returns>The newer version, or null if there is none or the check failed.</returns>
    Task<Version?> NewerVersionAsync(string feedUrl, Version current, CancellationToken cancellationToken);
}
