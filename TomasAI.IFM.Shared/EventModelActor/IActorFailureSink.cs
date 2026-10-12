using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public interface IActorFailureSink
{
    Guid RecordFailure(
        ActorMailboxId actorId,
        ActorThreadId threadId,
        string verb,
        ActorFailureStage stage,
        Exception exception,
        Guid? primaryFailureId = null,
        ActorMessageOutcomeType outcome = ActorMessageOutcomeType.EscapedFailure,
        ActorDeliveryOutcomeType deliveryOutcome = ActorDeliveryOutcomeType.NotRequired);
}
