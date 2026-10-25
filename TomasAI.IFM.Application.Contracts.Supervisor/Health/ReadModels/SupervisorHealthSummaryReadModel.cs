using MessagePack;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

/// <summary>Compact operational summary of the latest stored actor-health collection.</summary>
[MessagePackObject]
public sealed record SupervisorHealthSummaryReadModel(
    [property: Key(0)] long Revision,
    [property: Key(1)] DateTime ObservedUtc,
    [property: Key(2)] int ExpectedActors,
    [property: Key(3)] int CollectedActors,
    [property: Key(4)] int FailedActors,
    [property: Key(5)] int HealthyActors,
    [property: Key(6)] int DegradedActors,
    [property: Key(7)] int CriticalActors,
    [property: Key(8)] int UnknownActors,
    [property: Key(9)] SupervisorSnapshotQuality Quality);
