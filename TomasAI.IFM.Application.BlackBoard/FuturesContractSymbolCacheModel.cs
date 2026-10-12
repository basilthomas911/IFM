using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures contract symbol blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesContractSymbolCacheModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
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
        ArgumentNullException.ThrowIfNull(getFuturesContractSymbol);
        var key = $"{CacheName}:{contractId}";
        var value = await _redisCache.GetAsync(key).ConfigureAwait(false);
        if (string.IsNullOrEmpty(value))
        {
            var symbol = await (getFuturesContractSymbol(contractId)).ConfigureAwait(false);
            if (string.IsNullOrEmpty(symbol))
                return string.Empty;
            value = _jsonSerializer.Serialize(symbol);
            await _redisCache.SetAsync(key, value).ConfigureAwait(false);
        }
        return _jsonSerializer.Deserialize<string>(value) ?? string.Empty;
    }
}
