using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// domain events blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class DomainEventsModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.DomainEvents}";

    /// <summary>
    /// return cached domain events collection
    /// </summary>
    /// <param name="commandId"></param>
    /// <returns></returns>
    public DomainEventCollection Get(Guid commandId)
    {
        var key = $"{CacheName}:{commandId}";
        var value = redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? jsonSerializer.Deserialize<DomainEventCollection>(value) ?? []
            : [];
    }

    /// <summary>
    /// cache domain events collection
    /// </summary>
    /// <param name="commandId"></param>
    /// <param name="domainEvents"></param>
    public void Set(Guid commandId, DomainEventCollection domainEvents)
    {
        var key = $"{CacheName}:{commandId}";
        var value = jsonSerializer.Serialize(domainEvents);
        redisCache.Set(key, value);
    }
}
