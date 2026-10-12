using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

internal sealed class MarketDataBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IMarketDataBlackboard
{
    public RiskFreeRateCacheModel RiskFreeRate { get; } = new(redisCache, jsonSerializer);
}
