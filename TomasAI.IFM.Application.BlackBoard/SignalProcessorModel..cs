using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Util;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// signal processor model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class SignalProcessorModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.SignalProcessor}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached  signal
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <returns></returns>
    public SignalProcessor<TSignal>? Get<TSignal>(OptionTradeEntityId optionTradeId)
    {
        var key = $"{CacheName}:{optionTradeId.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<SignalProcessor<TSignal>>(value)
            : default;
    }

    /// <summary>
    /// cache  signal processor
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <param name="signalProcessor"></param>
    public void Set<TSignal>(OptionTradeEntityId optionTradeId, SignalProcessor<TSignal> signalProcessor)
    {
        var key = $"{CacheName}:{optionTradeId.Format()}";
        var value = _jsonSerializer.Serialize(signalProcessor);
        _redisCache.Set(key, value);
    }

    /// <summary>
    /// check if signal processor exists in cache
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <returns></returns>
    public bool Exists(OptionTradeEntityId optionTradeId)
    {
        var key = $"{CacheName}:{optionTradeId.Format()}";
        return _redisCache.TryGet(key, out _);
    }
}
