using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Recovery.Event.Projection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Event.Actor;

/// <summary>Restricts canary event handlers to projection and logging capabilities.</summary>
public interface IRecoveryCanaryEventContext : IEventActorContext<RecoveryCanaryProjectorActor>
{
    RecoveryCanaryProjectionStore Projection { get; }
    ILogger<RecoveryCanaryProjectorActor> Logger { get; }
}
