using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// futures option quote contract blackboard model constructor
/// </summary>
/// <param name="redisCache"></param>
/// <param name="jsonSerializer"></param>
public class FuturesOptionQuoteModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
{
    readonly string CacheName = $"{DataCacheName.FuturesOptionQuote}";
    readonly IRedisCache _redisCache = redisCache;
    readonly IJsonSerializer _jsonSerializer = jsonSerializer;

    /// <summary>
    /// return cached futures option quote map
    /// </summary>
    /// <param name="quoteId"></param>
    /// <returns>futures option quote map</returns>
    public Dictionary<int, FuturesOptionQuoteReadModel> Get(int quoteId)
    {
        var key = $"{CacheName}:{quoteId }";
        var value = _redisCache.Get(key);
        if (!string.IsNullOrEmpty(value))
        {
            var futuresOptionQuotes = _jsonSerializer.Deserialize<FuturesOptionQuoteReadModel[]>(value);
            if (futuresOptionQuotes is not null)
                return GetFuturesOptionContractMap(futuresOptionQuotes);
        }
        return [];

        static Dictionary<int, FuturesOptionQuoteReadModel> GetFuturesOptionContractMap(FuturesOptionQuoteReadModel[] futuresOptionQuotes)
        {
            Dictionary<int, FuturesOptionQuoteReadModel> quoteMap = [];
            foreach (var e in futuresOptionQuotes)
                quoteMap.Add(e.RequestId, e);
            return quoteMap;
        }
    }

    /// <summary>
    /// cache futures option quote contracts
    /// </summary>
    /// <param name="quoteId"></param>
    /// <param name="futuresOptionQuotes"></param>
    public void Set(int quoteId, FuturesOptionQuoteReadModel[] futuresOptionQuotes)
    {
        var key = $"{CacheName}:{quoteId}";
        var value = _jsonSerializer.Serialize(futuresOptionQuotes);
        _redisCache.Set(key, value);
    }

    /// <summary>
    /// clear futures option quote cache
    /// </summary>
    /// <param name="quoteId"></param>
    public void Clear(int quoteId)
    {
        var key = $"{CacheName}:{quoteId}";
        _redisCache.Set(key, string.Empty);
    }
}
