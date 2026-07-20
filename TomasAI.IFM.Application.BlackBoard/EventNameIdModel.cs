using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// event name id model    
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class EventNameIdModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.EventNameId}";

    /// <summary>
    /// for BDD tests useage only
    /// </summary>
    public EventNameIdModel() : this(default!, default!)
    {
    }

    /// <summary>
    /// return cached event name id
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventTypeName"></param>
    /// <param name="getEventNameId"></param>
    /// <returns></returns>
    public async Task<EventNameIdReadModel> GetAsync(string eventName, string eventTypeName, Func<string, string, Task<EventNameIdReadModel>> getEventNameId)
    {
        //var key = jsonSerializer.Serialize(new { CacheName, eventName, eventTypeName });
        var key = $"{CacheName}:{eventName}.{eventTypeName}";
        if (!redisCache.TryGet(key, out string? value))
        {
            var eventNameId = await getEventNameId(eventName, eventTypeName);
            value = jsonSerializer.Serialize(eventNameId);
            redisCache.Set(key, value);
        }
        return string.IsNullOrEmpty(value) 
            ? new EventNameIdReadModel(-1, string.Empty, string.Empty) 
            : jsonSerializer.Deserialize<EventNameIdReadModel>(value);
    }
}
