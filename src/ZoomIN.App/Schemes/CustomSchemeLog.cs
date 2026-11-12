using Microsoft.Extensions.Logging;
using ZoomIN.Core.Filters;

namespace ZoomIN.App.Schemes;

/// <summary>
/// Log messages of the own colour schemes (event ids 1620–1639, M18): kind and place only, never the
/// name. File errors are logged by exception type only: their messages hold the full path with the
/// user and profile name (AGENTS.md 2.5).
/// </summary>
internal static partial class CustomSchemeLog
{
    [LoggerMessage(EventId = 1620, Level = LogLevel.Information, Message = "Own scheme created in {Place} ({Kind})")]
    public static partial void Created(this ILogger logger, FilterId place, SchemeKind kind);

    [LoggerMessage(EventId = 1621, Level = LogLevel.Information, Message = "Own scheme in {Place} changed ({Kind}, {Replacements} replacements)")]
    public static partial void Changed(this ILogger logger, FilterId place, SchemeKind kind, int replacements);

    [LoggerMessage(EventId = 1622, Level = LogLevel.Information, Message = "Own scheme in {Place} deleted ({Kind})")]
    public static partial void Deleted(this ILogger logger, FilterId place, SchemeKind kind);

    [LoggerMessage(EventId = 1623, Level = LogLevel.Information, Message = "{Count} choices of the deleted scheme in {Place} fell back to {Fallback} ({Profiles} stored profiles changed)")]
    public static partial void FellBack(this ILogger logger, int count, FilterId place, FilterId fallback, int profiles);

    [LoggerMessage(EventId = 1624, Level = LogLevel.Warning, Message = "No free place for another own scheme ({Max} in use)")]
    public static partial void Full(this ILogger logger, int max);

    [LoggerMessage(EventId = 1625, Level = LogLevel.Warning, Message = "A stored profile could not be updated after deleting the scheme in {Place}: {Error}")]
    public static partial void ProfileNotUpdated(this ILogger logger, FilterId place, string error);

    [LoggerMessage(EventId = 1626, Level = LogLevel.Warning, Message = "Saving the settings after deleting the scheme in {Place} failed ({Error}); the next change saves them")]
    public static partial void SaveFailed(this ILogger logger, FilterId place, string error);
}
