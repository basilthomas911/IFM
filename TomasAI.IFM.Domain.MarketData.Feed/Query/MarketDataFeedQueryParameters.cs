using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

internal record MarketDataFeedQueryParameters
{
    public IMarketDataSnapshotApi MarketDataSnapshotApi { get; init; }
    public ISequenceIdGenerator SequenceIdGenerator { get; init; }
    public IDbContextFactory DbFactory { get; init; }

    public MarketDataFeedQueryParameters(
        IMarketDataSnapshotApi marketDataSnapshotApi,
        ISequenceIdGenerator sequenceIdGenerator,
        IDbContextFactory dbFactory)
    {
        MarketDataSnapshotApi = marketDataSnapshotApi;
        SequenceIdGenerator = sequenceIdGenerator;
        DbFactory = dbFactory;
    }
}
