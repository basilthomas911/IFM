using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventProjector.ReadModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// domain events blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class EventProjectorStateModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    /// <summary>
    /// Gets the cache name for the event projector state.
    /// </summary>
    readonly string CacheName = $"{DataCacheName.EventProjectorState}";

    /// <summary>
    /// Gets the event projector state from the cache for a given event ID.
    /// </summary>
    /// <param name="eventId"></param>
    /// <returns></returns>
    public EventProjectorStateReadModel Get(long eventId)
    {
        var key = $"{CacheName}:{eventId}";
        var value = redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? jsonSerializer.Deserialize<EventProjectorStateReadModel>(value) ?? default
            : default;
    }

    /// <summary>
    /// Sets the event projector state in the cache for a given event ID.
    /// </summary>
    /// <param name="eventId"></param>
    /// <param name="eventProjectorState"></param>
    public void Set(long eventId, EventProjectorStateReadModel eventProjectorState)
    {
        var key = $"{CacheName}:{eventId}";
        var value = jsonSerializer.Serialize(eventProjectorState);
        redisCache.Set(key, value);
    }

    /// <summary>
    /// Clears the event projector state from the cache for a given event ID.
    /// </summary>
    /// <param name="eventId"></param>
    public void Clear(long eventId)
    {
        var key = $"{CacheName}:{eventId}";
        redisCache.Remove(key);
    }
}
