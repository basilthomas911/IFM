using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public enum ActorDeliveryOutcomeType
{
    NotRequired = 0,
    Succeeded = 1,
    Failed = 2,
    Deferred = 3,
    Redelivered = 4,
    Terminal = 5
}
