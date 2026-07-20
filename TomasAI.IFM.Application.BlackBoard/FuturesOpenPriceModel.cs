using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures open price blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesOpenPriceModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesOpenPrice}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures open price
    /// </summary>
    /// <param name="futuresDataId"></param>
    /// <returns>futures open price</returns>
    public async ValueTask<decimal> GetAsync(FuturesDataId futuresDataId, Func<FuturesDataId, Task<decimal>> GetFuturesOpenPriceAsync)
    {
        var key = $"{CacheName}:{futuresDataId.Format()}";
        var value = _redisCache.Get(key);
        return !string.IsNullOrEmpty(value)
            ? Convert.ToDecimal(value)
            : await GetFuturesOpenPriceValueAsync();

        async Task<decimal> GetFuturesOpenPriceValueAsync()
        {
            var futuresOpenPrice = await GetFuturesOpenPriceAsync(futuresDataId);
            _redisCache.Set(key, $"{futuresOpenPrice}");
            return futuresOpenPrice;
        }
    }

    /// <summary>
    /// clear futures open price cache
    /// </summary>
    /// <param name="futuresDataId"></param>
    public void Clear(FuturesDataId futuresDataId)
    {
        var key = $"{CacheName}:{futuresDataId.Format()}";
        _redisCache.Set(key, string.Empty);
    }
}
