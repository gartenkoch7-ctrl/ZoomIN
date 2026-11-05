using System.Text.Json.Serialization;
using ZoomIN.Core.Settings;

namespace ZoomIN.Platform.Settings;

/// <summary>Source-generated JSON metadata: no reflection, trimming-safe (ADR-0004).</summary>
// Read-only properties are computed (e.g. WindowFilterOptions.ThresholdPercent) and stay out of the file.
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, IgnoreReadOnlyProperties = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
