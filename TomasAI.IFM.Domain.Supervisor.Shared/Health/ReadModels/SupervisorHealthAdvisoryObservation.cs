using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

/// <summary>Bounded observation reserved for a future fire-and-forget advisory sink.</summary>
public sealed record SupervisorHealthAdvisoryObservation(
    long Revision,
    DateTime ObservedUtc,
    int HealthyActors,
    int DegradedActors,
    int CriticalActors,
    int UnknownActors);
