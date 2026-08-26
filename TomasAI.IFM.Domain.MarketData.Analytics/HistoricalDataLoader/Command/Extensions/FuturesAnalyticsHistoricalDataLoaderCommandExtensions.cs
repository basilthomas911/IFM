using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Extensions;

/// <summary>Exposes historical data-load Command services as readonly extension properties.</summary>
public static class FuturesAnalyticsHistoricalDataLoaderCommandExtensions
{
    extension(ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesAnalyticsHistoricalDataLoaderCommandContext DataLoaderContext =>
            context as IFuturesAnalyticsHistoricalDataLoaderCommandContext
            ?? throw new InvalidOperationException("The data load Command actor requires its typed context.");
        /// <summary>Gets the state repository.</summary>
        public IEventSourceActorStateRepository<FuturesAnalyticsHistoricalDataLoaderCommandState> DataLoaderRepository =>
            context.DataLoaderContext.Repository;
        /// <summary>Gets the durable event projector.</summary>
        public IEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor> DataLoaderProjector =>
            context.DataLoaderContext.EventProjector;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesAnalyticsHistoricalDataLoaderCommandActor> Logger => context.DataLoaderContext.Logger;
    }
}
