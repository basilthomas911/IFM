using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Actor;

/// <summary>Defines readonly services required by the historical data-load Command actor.</summary>
public interface IFuturesAnalyticsHistoricalDataLoaderCommandContext
    : ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor>
{
    /// <summary>Gets the typed state repository.</summary>
    IEventSourceActorStateRepository<FuturesAnalyticsHistoricalDataLoaderCommandState> Repository { get; }
    /// <summary>Gets the durable event projector.</summary>
    IEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor> EventProjector { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesAnalyticsHistoricalDataLoaderCommandActor> Logger { get; }
}
