using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// Provides domain-grouped access to application cache models.
/// </summary>
public class BlackboardService : IBlackboardService
{
    public BlackboardService(IRedisCache redisCache, IJsonSerializer jsonSerializer)
    {
        IsArgumentNull.Check(redisCache);
        IsArgumentNull.Check(jsonSerializer);

        EventSourcing = new EventSourcingBlackboard(redisCache, jsonSerializer);
        Fund = new FundBlackboard(redisCache, jsonSerializer);
        MarketData = new MarketDataBlackboard(redisCache, jsonSerializer);
        MarketDataAnalytics = new MarketDataAnalyticsBlackboard(redisCache, jsonSerializer);
        MarketDataFeed = new MarketDataFeedBlackboard(redisCache, jsonSerializer);
        MarketDataSecurities = new MarketDataSecuritiesBlackboard(
            redisCache,
            jsonSerializer);
        Reference = new ReferenceBlackboard(redisCache, jsonSerializer);
        Trade = new TradeBlackboard(redisCache, jsonSerializer);
    }

    public IEventSourcingBlackboard EventSourcing { get; }
    public IFundBlackboard Fund { get; }
    public IMarketDataBlackboard MarketData { get; }
    public IMarketDataAnalyticsBlackboard MarketDataAnalytics { get; }
    public IMarketDataFeedBlackboard MarketDataFeed { get; }
    public IMarketDataSecuritiesBlackboard MarketDataSecurities { get; }
    public IReferenceBlackboard Reference { get; }
    public ITradeBlackboard Trade { get; }
}
