using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public interface IMarketDataAnalyticsBlackboard
{
    FuturesItiSignalAveragePredictedTrendDeltaCacheModel
        FuturesItiSignalAveragePredictedTrendDelta
    { get; }
    FuturesItiSignalAveragePredictedTrendDeltaRangeCacheModel
        FuturesItiSignalAveragePredictedTrendDeltaRange
    { get; }
    FuturesItiSignalMDICacheModel FuturesItiSignalMDI { get; }
    FuturesRsiSignalCacheModel FuturesRsiSignal { get; }
    FuturesRsiDailySignalCacheModel FuturesRsiDailySignal { get; }
}
