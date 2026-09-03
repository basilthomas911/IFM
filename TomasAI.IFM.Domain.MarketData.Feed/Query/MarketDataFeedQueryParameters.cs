using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

internal record MarketDataFeedQueryParameters
{
    public ApplicationMarketDataApi MarketDataApi { get; init; }
    public ISequenceIdGenerator SequenceIdGenerator { get; init; }
    public IDbContextFactory DbFactory { get; init; }
    public IMarketDataServiceStore MarketDataServiceStore { get; init; }
    public IMarketDataLifecycleRequests MarketDataLifecycle { get; init; }

    public MarketDataFeedQueryParameters(
        ApplicationMarketDataApi marketDataApi,
        ISequenceIdGenerator sequenceIdGenerator,
        IDbContextFactory dbFactory,
        IMarketDataServiceStore marketDataServiceStore,
        IMarketDataLifecycleRequests marketDataLifecycle)
    {
        MarketDataApi = marketDataApi;
        SequenceIdGenerator = sequenceIdGenerator;
        DbFactory = dbFactory;
        MarketDataServiceStore = marketDataServiceStore;
        MarketDataLifecycle = marketDataLifecycle;
    }
}
