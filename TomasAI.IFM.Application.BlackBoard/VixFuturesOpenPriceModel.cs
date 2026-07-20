using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// vix futures open price blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class VixFuturesOpenPriceModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.VixFuturesOpenPrice}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached vix futures open price
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns>futures open price</returns>
    public async ValueTask<decimal> GetAsync(VixFuturesEodDataEntityId entityId, Func<VixFuturesEodDataEntityId, Task<decimal>> GetVixFuturesOpenPriceAsync)
    {
        var key = $"{CacheName}:{entityId.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? Convert.ToDecimal(value)
            : await GetVixFuturesOpenPriceValueAsync();

        async Task<decimal> GetVixFuturesOpenPriceValueAsync()
        {
            var vixFuturesOpenPrice = await GetVixFuturesOpenPriceAsync(entityId);
            _redisCache.Set(key, $"{vixFuturesOpenPrice}");
            return vixFuturesOpenPrice;
        }
    }

    /// <summary>
    /// clear vix futures open price cache
    /// </summary>
    /// <param name="entityId"></param>
    public void Clear(VixFuturesEodDataEntityId entityId)
    {
        var key = $"{CacheName}:{entityId.Format()}";
        _redisCache.Set(key, string.Empty);
    }
}
