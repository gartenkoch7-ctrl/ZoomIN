using System.Text.Json.Serialization;
using ZoomIN.Core.Settings;

namespace ZoomIN.Platform.Settings;

/// <summary>Source-generated JSON metadata: no reflection, trimming-safe (ADR-0004).</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
