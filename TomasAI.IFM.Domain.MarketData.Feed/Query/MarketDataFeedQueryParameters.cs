using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public sealed record MarketDataFeedQueryParameters
{
    public ApplicationMarketDataApi MarketDataApi { get; init; }
    public IDbContextFactory DbFactory { get; init; }
    public IMarketDataServiceStore MarketDataServiceStore { get; init; }
    public TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi MarketDataLifecycle { get; init; }

    public MarketDataFeedQueryParameters(
        ApplicationMarketDataApi marketDataApi,
        IDbContextFactory dbFactory,
        IMarketDataServiceStore marketDataServiceStore,
        TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi marketDataLifecycle)
    {
        MarketDataApi = marketDataApi;
        DbFactory = dbFactory;
        MarketDataServiceStore = marketDataServiceStore;
        MarketDataLifecycle = marketDataLifecycle;
    }
}
