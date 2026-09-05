namespace TomasAI.IFM.Application.MarketData.UnitTests;

// Process-wide allocation/handle measurements cannot run concurrently with tests that create
// native worker processes and pipes. Keep the existing resource limits, isolate their measurement.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessResourceQualificationCollection
{
    public const string Name = "Process-wide resource qualification";
}
