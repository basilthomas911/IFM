using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using HistoricalDataLoaderService = TomasAI.IFM.Application.MarketData.Historical.HistoricalDataLoader;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Extensions;

/// <summary>Exposes data load Event services as readonly extension properties.</summary>
public static class FuturesAnalyticsHistoricalDataLoaderEventExtensions
{
    extension(IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesAnalyticsHistoricalDataLoaderEventContext DataLoaderContext =>
            context as IFuturesAnalyticsHistoricalDataLoaderEventContext
            ?? throw new InvalidOperationException("The data load Event actor requires its typed context.");
        /// <summary>Gets the data load coordinator.</summary>
        public HistoricalDataLoaderService DataLoader => context.DataLoaderContext.DataLoader;
        /// <summary>Gets the data load operational store.</summary>
        public IHistoricalDataLoaderStore DataLoaderStore => context.DataLoaderContext.DataLoaderStore;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> Logger => context.DataLoaderContext.Logger;
    }
}
