using System.Text.RegularExpressions;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Architecture.Tests;

/// <summary>
/// Telemetry rules of ADR-0019, checked on the source code so the WinUI projects (App, Setup) are
/// covered too: one source and meter, the naming scheme, unique log event ids in the range of
/// their project, no user content in attribute keys or log placeholders, no console output.
/// </summary>
public sealed partial class TelemetryConventionTests
{
    // Event id ranges per project (docs/kb/telemetrie.md).
    private static readonly Dictionary<string, (int Min, int Max)> s_eventRanges = new(StringComparer.Ordinal)
    {
        ["ZoomIN.App"] = (1000, 1999),
        ["ZoomIN.Platform"] = (2000, 2999),
        ["ZoomIN.Rendering"] = (3000, 3999),
        ["ZoomIN.Watchdog"] = (4000, 4999),
        ["ZoomIN.Setup"] = (5000, 5999),
        ["ZoomIN.Core"] = (6000, 6999),
    };

    [Fact]
    public void OnlyCore_CreatesTheActivitySourceAndMeter()
    {
        var offenders = SourceFiles.Production()
            .Where(file => !file.Path.EndsWith(Path.Combine("Core", "Diagnostics", "Telemetry.cs"), StringComparison.Ordinal))
            .Where(file => NewSourceOrMeter().IsMatch(file.Text))
            .Select(file => file.Path)
            .ToList();

        Assert.True(offenders.Count == 0, "Use Telemetry.Source / Telemetry.Meter: " + string.Join(", ", offenders));
    }

    [Fact]
    public void OperationAndInstrumentNames_FollowTheScheme()
    {
        var names = SourceFiles.Production()
            .SelectMany(file => NamedTelemetry().Matches(file.Text).Select(m => (file.Path, Name: m.Groups["name"].Value)))
            .ToList();
        var offenders = names
            .Where(found => !NameScheme().IsMatch(found.Name))
            .Select(found => $"{found.Name} ({Path.GetFileName(found.Path)})")
            .ToList();

        Assert.Contains(names, found => found.Name == "zoomin.app.start");
        Assert.True(offenders.Count == 0, "Names must be zoomin.<feature>.<operation>: " + string.Join(", ", offenders));
    }

    [Fact]
    public void LogEventIds_AreUnique_AndInTheirProjectRange()
    {
        var ids = SourceFiles.Production()
            .SelectMany(file => EventIdDeclaration().Matches(file.Text).Select(m => (file.Project, Id: int.Parse(m.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture))))
            .ToList();

        var duplicates = ids.GroupBy(found => found.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        var outOfRange = ids
            .Where(found => !s_eventRanges.TryGetValue(found.Project, out var range) || found.Id < range.Min || found.Id > range.Max)
            .Select(found => $"{found.Id} ({found.Project})")
            .ToList();

        Assert.NotEmpty(ids);
        Assert.True(duplicates.Count == 0, "Duplicate event ids: " + string.Join(", ", duplicates));
        Assert.True(outOfRange.Count == 0, "Event id outside its project's range: " + string.Join(", ", outOfRange));
    }

    [Fact]
    public void AttributeKeysAndLogPlaceholders_CarryNoUserContent()
    {
        var keys = SourceFiles.Production().SelectMany(file =>
            TagKey().Matches(file.Text).Select(m => m.Groups["key"].Value)
                .Concat(LogMessage().Matches(file.Text).SelectMany(m => Placeholder().Matches(m.Groups["message"].Value).Select(p => p.Groups["name"].Value))));

        Assert.Contains("Gesture", keys);

        var offenders = keys.Where(key => !TelemetryPrivacy.IsAllowedAttribute(key)).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(offenders.Count == 0, "Possible user content in telemetry: " + string.Join(", ", offenders));
    }

    [Fact]
    public void NoConsoleOrDebugOutput()
    {
        var offenders = SourceFiles.Production()
            .Where(file => ConsoleOutput().IsMatch(file.Text))
            .Select(file => file.Path)
            .ToList();

        Assert.True(offenders.Count == 0, "Log through ILogger instead: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"new\s+(ActivitySource|Meter)\s*\(")]
    private static partial Regex NewSourceOrMeter();

    [GeneratedRegex(@"(StartOperation|StartActivity|Create(Counter|Histogram|UpDownCounter|Gauge|ObservableCounter|ObservableGauge|ObservableUpDownCounter)(<[^>]+>)?)\(\s*""(?<name>[^""]+)""")]
    private static partial Regex NamedTelemetry();

    [GeneratedRegex(@"^zoomin(\.[a-z][a-z0-9_]*){2,}$")]
    private static partial Regex NameScheme();

    [GeneratedRegex(@"\[LoggerMessage\(\s*EventId\s*=\s*(?<id>\d+)")]
    private static partial Regex EventIdDeclaration();

    [GeneratedRegex(@"SetTag\(\s*""(?<key>[^""]+)""")]
    private static partial Regex TagKey();

    [GeneratedRegex(@"\[LoggerMessage\([^\]]*Message\s*=\s*""(?<message>[^""]*)""")]
    private static partial Regex LogMessage();

    [GeneratedRegex(@"\{(?<name>[A-Za-z][A-Za-z0-9_.]*)(:[^}]*)?\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\b(Console\.(Write|WriteLine|Error)|Debug\.(Write|WriteLine|Print)|Trace\.(Write|WriteLine))\b")]
    private static partial Regex ConsoleOutput();
}
