using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

/// <summary>Immutable current incident for one unhealthy actor thread.</summary>
public sealed record SupervisorActorIncident(
    ActorThreadId ThreadId,
    SupervisorActorHealth Health,
    DateTime FirstObservedUtc,
    DateTime LastObservedUtc,
    long Generation,
    int QueueDepth,
    int QueueCapacity,
    int RestartsIn60Minutes = 0,
    int RestartsIn24Hours = 0,
    bool RequiresTraderReview = false);
