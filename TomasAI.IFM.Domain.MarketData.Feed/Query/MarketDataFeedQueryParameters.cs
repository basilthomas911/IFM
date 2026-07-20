using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

internal record MarketDataFeedQueryParameters
{
    public IMarketDataSnapshotApi MarketDataSnapshotApi { get; init; }
    public IBlackboardService BlackboardService { get; init; }
    public IDbContextFactory DbFactory { get; init; }

    public MarketDataFeedQueryParameters(
        IMarketDataSnapshotApi marketDataSnapshotApi,
        IBlackboardService blackboardService,
        IDbContextFactory dbFactory)
    {
        MarketDataSnapshotApi = marketDataSnapshotApi;
        BlackboardService = blackboardService;
        DbFactory = dbFactory;
    }
}
