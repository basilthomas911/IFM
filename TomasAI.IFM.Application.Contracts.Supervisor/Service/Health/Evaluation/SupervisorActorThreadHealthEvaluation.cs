using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Policy;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Evaluation;

/// <summary>Result of one baseline actor-thread saturation evaluation.</summary>
public sealed record SupervisorActorThreadHealthEvaluation(
    ActorThreadId ThreadId,
    SupervisorActorHealth Health,
    TimeSpan ContinuousLimitDuration,
    bool LogWarning,
    bool RestartRequired,
    bool Recovered,
    SupervisorMailboxPressureState PressureState = SupervisorMailboxPressureState.Normal,
    bool NoProgressWarning = false,
    bool HandlerDurationWarning = false);
