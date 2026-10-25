using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

/// <summary>Combines Market Data Service database read and write capabilities.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3444", Justification = "Keep the published context interface and named argument contract; inherited capability members intentionally describe the same database operation.")]
public interface IMarketDataServiceDbContext : IObjectRepository<MarketDataServiceDbContext>, IMarketDataServiceDbReadContext, IMarketDataServiceDbWriteContext, IMarketDataServiceStore, ICompositionRoutePlanStore, IDurableSubscriptionIntentStore
{
    /// <summary>Gets the Market Data Service database read capability.</summary>
    IMarketDataServiceDbReadContext DbReader { get; }
    /// <summary>Gets the Market Data Service database write capability.</summary>
    IMarketDataServiceDbWriteContext DbWriter { get; }
}
