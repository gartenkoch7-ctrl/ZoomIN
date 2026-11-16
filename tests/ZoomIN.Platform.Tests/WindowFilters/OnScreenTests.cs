namespace ZoomIN.Platform.Tests.WindowFilters;

/// <summary>Tests with on-screen test windows run one after another, so their windows never overlap.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OnScreenTests
{
    public const string Name = "Test windows";
}
