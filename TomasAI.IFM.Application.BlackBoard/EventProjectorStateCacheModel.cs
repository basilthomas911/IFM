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
public class EventProjectorStateCacheModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    /// <summary>
    /// Gets the cache name for the event projector state.
    /// </summary>
    readonly string CacheName = $"{DataCacheName.EventProjectorState}";

    /// <summary>
    /// Gets the event projector state from the cache for a given event and projector.
    /// </summary>
    /// <param name="eventId"></param>
    /// <param name="projectorName">The projector that owns the independent projection state.</param>
    /// <returns>The cached state, or <see langword="null"/> when no state is cached.</returns>
    public EventProjectorStateReadModel? Get(long eventId, string projectorName)
    {
        var key = GetKey(eventId, projectorName);
        var value = redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? jsonSerializer.Deserialize<EventProjectorStateReadModel>(value)
            : null;
    }

    /// <summary>
    /// Sets the event projector state in the cache for a given event and projector.
    /// </summary>
    /// <param name="eventId"></param>
    /// <param name="projectorName">The projector that owns the independent projection state.</param>
    /// <param name="eventProjectorState"></param>
    public void Set(long eventId, string projectorName, EventProjectorStateReadModel eventProjectorState)
    {
        var key = GetKey(eventId, projectorName);
        var value = jsonSerializer.Serialize(eventProjectorState);
        redisCache.Set(key, value);
    }

    /// <summary>
    /// Clears the event projector state from the cache for a given event and projector.
    /// </summary>
    /// <param name="eventId"></param>
    /// <param name="projectorName">The projector that owns the independent projection state.</param>
    public void Clear(long eventId, string projectorName)
    {
        var key = GetKey(eventId, projectorName);
        redisCache.Remove(key);
    }

    string GetKey(long eventId, string projectorName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        return $"{CacheName}:{projectorName}:{eventId}";
    }
}
