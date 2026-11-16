namespace ZoomIN.Core.Updates;

/// <param name="Result">Whether the package arrived intact.</param>
/// <param name="Path">The verified package file; null unless <paramref name="Result"/> is <see cref="PackageCheck.Ok"/>.</param>
public sealed record PackageDownload(PackageCheck Result, string? Path);
