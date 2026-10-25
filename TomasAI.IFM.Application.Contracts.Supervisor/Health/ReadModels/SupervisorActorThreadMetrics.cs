using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

/// <summary>Immutable metrics for one actor entity mailbox.</summary>
public sealed record SupervisorActorThreadMetrics(
    ActorThreadId ThreadId,
    int QueueDepth,
    int QueueCapacity,
    int PeakQueueDepth,
    TimeSpan OldestMessageAge,
    long Accepted,
    long Dequeued,
    long Succeeded,
    long Failed,
    long Cancelled,
    long Rejected,
    string? CurrentVerb,
    string? CurrentStage,
    TimeSpan CurrentElapsed,
    SupervisorActorHealth Health,
    TimeSpan ContinuousLimitDuration = default,
    bool RestartRequired = false,
    long Generation = 0,
    SupervisorMailboxPressureState PressureState = SupervisorMailboxPressureState.Normal,
    double Utilization = 0,
    int RemainingCapacity = 0,
    DateTime? LastProgressUtc = null,
    bool HasProgress = true,
    bool HandlerDurationWarning = false,
    bool NoProgressWarning = false);
