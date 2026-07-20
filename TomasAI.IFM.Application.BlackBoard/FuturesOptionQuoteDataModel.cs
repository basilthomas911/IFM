using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Blackboard;

public class FuturesOptionQuoteDataModel
{
    public readonly string CacheName = $"{DataCacheName.FuturesOptionQuoteData}";
    readonly IRedisCache? _redisCache;
    readonly IJsonSerializer _jsonSerializer;

    /// <summary>
    /// for BDD testing only
    /// </summary>
    public FuturesOptionQuoteDataModel() : this(default!, default!)
    {
    }

    /// <summary>
    /// futures option quote data blackboard model constructor
    /// </summary>
    /// <param name="redisCache"></param>
    /// <param name="jsonSerializer"></param>
    public FuturesOptionQuoteDataModel(IRedisCache redisCache, IJsonSerializer jsonSerializer)
    {
        _redisCache = redisCache;
        _jsonSerializer = jsonSerializer;
    }

    /// <summary>
    /// return cached futures option quote data
    /// </summary>
    /// <param name="optionQuoteId"></param>
    /// <returns>futures option quote map</returns>
    public FuturesOptionQuoteDataReadModel? Get(FuturesOptionQuoteId optionQuoteId)
    {
        var key = $"{CacheName}:{optionQuoteId.Format()}";
        var value = _redisCache!.Get(key);
        return !string.IsNullOrEmpty(value)
            ? _jsonSerializer.Deserialize<FuturesOptionQuoteDataReadModel>(value)
            : default;
    }

    /// <summary>
    /// cache futures option quote data
    /// </summary>
    /// <param name="optionQuoteId"></param>
    /// <param name="futuresOptionQuoteData"></param>
    public void Set(FuturesOptionQuoteId optionQuoteId, FuturesOptionQuoteDataReadModel futuresOptionQuoteData) 
    {
        var key = $"{CacheName}:{optionQuoteId.Format()}";
        var value = _jsonSerializer.Serialize(futuresOptionQuoteData);
        _redisCache!.Set(key, value);
    }

    /// <summary>
    /// clear futures option quote cache
    /// </summary>
    /// <param name="optionQuoteId"></param>
    public void Clear(FuturesOptionQuoteId optionQuoteId)
    {
        var key = $"{CacheName}:{optionQuoteId.Format()}";
        _redisCache!.Set(key, string.Empty);
    }
}
