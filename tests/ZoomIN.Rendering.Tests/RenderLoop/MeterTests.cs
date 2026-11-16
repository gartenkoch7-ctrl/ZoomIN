namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>
/// Tests that record on the shared static <c>Telemetry.Meter</c> or listen to it run one after
/// another: a listener's callback runs on the recording thread, so a test listening in parallel
/// would add allocations to an allocation test or extra measurements to an exact-sequence check.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MeterTests
{
    public const string Name = "Shared meter";
}
