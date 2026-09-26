using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

/// <summary>Combines Market Data Service database read and write capabilities.</summary>
public interface IMarketDataServiceDbContext : IObjectRepository<MarketDataServiceDbContext>, IMarketDataServiceDbReadContext, IMarketDataServiceDbWriteContext, IMarketDataServiceStore, ICompositionRoutePlanStore, IDurableSubscriptionIntentStore
{
    /// <summary>Gets the Market Data Service database read capability.</summary>
    IMarketDataServiceDbReadContext DbReader { get; }
    /// <summary>Gets the Market Data Service database write capability.</summary>
    IMarketDataServiceDbWriteContext DbWriter { get; }
}
