using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures contract symbol blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesContractSymbolModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesContractSymbol}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures contract symbol
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async ValueTask<string> GetAsync(string contractId, Func<string, ValueTask<string>> getFuturesContractSymbol)
    {
        var key = $"{CacheName}:{contractId}";
        var value = _redisCache.Get(key);
        if (string.IsNullOrEmpty(value))
        {
            var symbol = await getFuturesContractSymbol(contractId);
            if (string.IsNullOrEmpty(symbol))
                return string.Empty;
            value = _jsonSerializer.Serialize(symbol);
            _redisCache.Set(key, value);
        }
        return _jsonSerializer.Deserialize<string>(value) ?? string.Empty;
    }
}
