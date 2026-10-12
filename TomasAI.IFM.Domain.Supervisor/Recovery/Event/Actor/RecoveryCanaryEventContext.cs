using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Recovery.Event.Projection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Event.Actor;

/// <summary>Provides the closed-generic recovery canary event context.</summary>
public sealed class RecoveryCanaryEventContext(IActorSupervisor supervisor,
    RecoveryCanaryProjectionStore projection, ILogger<RecoveryCanaryProjectorActor> logger)
    : EventActorContext(supervisor, new(ActorType.Event, RecoveryCanaryProjectorActor.ActorName)),
IRecoveryCanaryEventContext
{
    public RecoveryCanaryProjectionStore Projection { get; } = projection;
    public ILogger<RecoveryCanaryProjectorActor> Logger { get; } = logger;
}
