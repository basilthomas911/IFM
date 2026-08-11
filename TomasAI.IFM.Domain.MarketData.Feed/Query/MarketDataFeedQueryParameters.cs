using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.SequenceId;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

internal record MarketDataFeedQueryParameters
{
    public ApplicationMarketDataApi MarketDataApi { get; init; }
    public ISequenceIdGenerator SequenceIdGenerator { get; init; }
    public IDbContextFactory DbFactory { get; init; }

    public MarketDataFeedQueryParameters(
        ApplicationMarketDataApi marketDataApi,
        ISequenceIdGenerator sequenceIdGenerator,
        IDbContextFactory dbFactory)
    {
        MarketDataApi = marketDataApi;
        SequenceIdGenerator = sequenceIdGenerator;
        DbFactory = dbFactory;
    }
}
