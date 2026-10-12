using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public interface IEventSourcingBlackboard
{
    DomainEventsCacheModel DomainEvents { get; }
    EventStreamIdCacheModel EventStreamId { get; }
    EventNameIdCacheModel EventNameId { get; }
    EventProjectorStateCacheModel EventProjectorState { get; }
}
