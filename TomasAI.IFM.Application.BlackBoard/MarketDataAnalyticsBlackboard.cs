using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

internal sealed class MarketDataAnalyticsBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IMarketDataAnalyticsBlackboard
{
    public FuturesItiSignalAveragePredictedTrendDeltaCacheModel
        FuturesItiSignalAveragePredictedTrendDelta
    { get; } =
            new(redisCache, jsonSerializer);

    public FuturesItiSignalAveragePredictedTrendDeltaRangeCacheModel
        FuturesItiSignalAveragePredictedTrendDeltaRange
    { get; } =
            new(redisCache, jsonSerializer);

    public FuturesItiSignalMDICacheModel FuturesItiSignalMDI { get; } =
        new(redisCache, jsonSerializer);

    public FuturesRsiSignalCacheModel FuturesRsiSignal { get; } =
        new(redisCache, jsonSerializer);

    public FuturesRsiDailySignalCacheModel FuturesRsiDailySignal { get; } =
        new(redisCache, jsonSerializer);
}
