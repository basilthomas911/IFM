using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Actor;

/// <summary>Provides the closed generic context for the historical data-load Command actor.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesAnalyticsHistoricalDataLoaderCommandContext
    : CommandActorContext,
      IFuturesAnalyticsHistoricalDataLoaderCommandContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesAnalyticsHistoricalDataLoaderCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorStateRepository<FuturesAnalyticsHistoricalDataLoaderCommandState> repository,
        IEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor> eventProjector,
        ILogger<FuturesAnalyticsHistoricalDataLoaderCommandActor> logger)
        : base(supervisor, new(ActorType.Command, FuturesAnalyticsHistoricalDataLoaderCommandActor.ActorName))
    {
        Repository = IsArgumentNull.Set(repository);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IEventSourceActorStateRepository<FuturesAnalyticsHistoricalDataLoaderCommandState> Repository { get; }
    /// <inheritdoc />
    public IEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor> EventProjector { get; }
    /// <inheritdoc />
    public ILogger<FuturesAnalyticsHistoricalDataLoaderCommandActor> Logger { get; }
}
