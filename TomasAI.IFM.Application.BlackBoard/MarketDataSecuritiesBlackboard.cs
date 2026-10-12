using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

internal sealed class MarketDataSecuritiesBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IMarketDataSecuritiesBlackboard
{
    public IDatabentoContractMappingCache DatabentoContractMapping { get; } =
        new DatabentoContractMappingCache(redisCache, jsonSerializer);

    public FuturesContractCacheModel FuturesContract { get; } =
        new(redisCache, jsonSerializer);

    public FuturesContractSymbolCacheModel FuturesContractSymbol { get; } =
        new(redisCache, jsonSerializer);
}
