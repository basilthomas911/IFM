using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// vix futures open price blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class VixFuturesOpenPriceCacheModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.VixFuturesOpenPrice}";
    readonly IRedisCache _redisCache = redisCache;

    /// <summary>
    /// return cached vix futures open price
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns>futures open price</returns>
    public async ValueTask<decimal> GetAsync(VixFuturesEodDataEntityId entityId, Func<VixFuturesEodDataEntityId, Task<decimal>> GetVixFuturesOpenPriceAsync)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(GetVixFuturesOpenPriceAsync);
        var key = $"{CacheName}:{entityId.Format()}";
        var value = await _redisCache.GetAsync(key).ConfigureAwait(false);
        return !string.IsNullOrEmpty(value)
            ? Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture)
            : await (GetVixFuturesOpenPriceValueAsync()).ConfigureAwait(false);

        async Task<decimal> GetVixFuturesOpenPriceValueAsync()
        {
            var vixFuturesOpenPrice = await (GetVixFuturesOpenPriceAsync(entityId)).ConfigureAwait(false);
            await _redisCache.SetAsync(key, vixFuturesOpenPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);
            return vixFuturesOpenPrice;
        }
    }

    /// <summary>
    /// clear vix futures open price cache
    /// </summary>
    /// <param name="entityId"></param>
    public void Clear(VixFuturesEodDataEntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        var key = $"{CacheName}:{entityId.Format()}";
        _redisCache.Set(key, string.Empty);
    }
}
