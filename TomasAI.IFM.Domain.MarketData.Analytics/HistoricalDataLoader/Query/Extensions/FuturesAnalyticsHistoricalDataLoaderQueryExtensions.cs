using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Extensions;

/// <summary>Exposes data load Query services as readonly extension properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class FuturesAnalyticsHistoricalDataLoaderQueryExtensions
{
    extension(IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesAnalyticsHistoricalDataLoaderQueryContext DataLoaderContext =>
            context as IFuturesAnalyticsHistoricalDataLoaderQueryContext
            ?? throw new InvalidOperationException("The data load Query actor requires its typed context.");
        /// <summary>Gets the operational data load store.</summary>
        public IHistoricalDataLoaderStore DataLoaderStore => context.DataLoaderContext.DataLoaderStore;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesAnalyticsHistoricalDataLoaderQueryActor> Logger => context.DataLoaderContext.Logger;
    }
}
