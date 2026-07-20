using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// event stream id caching model    
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class EventStreamIdModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string cacheName = $"{DataCacheName.EventStreamId}";

    /// <summary>
    /// for BDD tests useage only
    /// </summary>
    public EventStreamIdModel() : this(default!, default!)
    {
    }

    /// <summary>
    /// return cached event stream id view model from event stream
    /// </summary>
    /// <param name="eventStream"></param>
    /// <param name="getEventStreamId"></param>
    /// <returns></returns>
    public async ValueTask<EventStreamIdReadModel> GetAsync(string eventStream, Func<string, Task<EventStreamIdReadModel>> getEventStreamId)
    {
        var key = $"{cacheName}:{eventStream}";
        if (!redisCache.TryGet(key, out string? value))
        {
            var eventStreamId = await getEventStreamId(eventStream);
            value = jsonSerializer.Serialize(eventStreamId);
            redisCache.Set(key, value);
        }
        return jsonSerializer.Deserialize<EventStreamIdReadModel>(value!) ?? new EventStreamIdReadModel(0, string.Empty);
    }

    /// <summary>
    /// Removes the cached entry associated with the specified event stream from the Redis cache.
    /// </summary>
    /// <param name="eventStream">The name of the event stream whose cached entry should be removed. Cannot be null or empty.</param>
    public void Remove(string eventStream)
    {
        var key = $"{cacheName}:{eventStream}";
        redisCache.Remove(key);
    }
}
