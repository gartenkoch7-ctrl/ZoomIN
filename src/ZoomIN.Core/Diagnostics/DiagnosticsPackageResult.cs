namespace ZoomIN.Core.Diagnostics;

/// <summary>A diagnostics package that was written.</summary>
/// <param name="Path">The ZIP file.</param>
/// <param name="SecureLogs">How adding the secure instance's logs went; null if they were not asked for.</param>
public sealed record DiagnosticsPackageResult(string Path, SecureLogsResult? SecureLogs);
