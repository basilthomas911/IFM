using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

internal sealed class EventSourcingBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IEventSourcingBlackboard
{
    public DomainEventsCacheModel DomainEvents { get; } = new(redisCache, jsonSerializer);
    public EventStreamIdCacheModel EventStreamId { get; } = new(redisCache, jsonSerializer);
    public EventNameIdCacheModel EventNameId { get; } = new(redisCache, jsonSerializer);
    public EventProjectorStateCacheModel EventProjectorState { get; } =
        new(redisCache, jsonSerializer);
}
