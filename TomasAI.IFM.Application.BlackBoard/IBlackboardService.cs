namespace TomasAI.IFM.Application.Blackboard;

public interface IBlackboardService
{
    IApplicationBlackboard Application { get; }
    IEventSourcingBlackboard EventSourcing { get; }
    IFundBlackboard Fund { get; }
    IMarketDataBlackboard MarketData { get; }
    IMarketDataAnalyticsBlackboard MarketDataAnalytics { get; }
    IMarketDataFeedBlackboard MarketDataFeed { get; }
    IMarketDataSecuritiesBlackboard MarketDataSecurities { get; }
    IReferenceBlackboard Reference { get; }
    ITradeBlackboard Trade { get; }
}
