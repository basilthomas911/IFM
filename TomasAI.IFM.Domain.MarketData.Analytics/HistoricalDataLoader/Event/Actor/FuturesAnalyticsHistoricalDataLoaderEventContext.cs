using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using HistoricalDataLoaderService = TomasAI.IFM.Application.MarketData.Historical.HistoricalDataLoader;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Actor;

/// <summary>Provides the closed generic context for the durable data load Event actor.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesAnalyticsHistoricalDataLoaderEventContext
    : EventActorContext,
      IFuturesAnalyticsHistoricalDataLoaderEventContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesAnalyticsHistoricalDataLoaderEventContext(
        IActorSupervisor supervisor,
        HistoricalDataLoaderService dataLoader,
        IHistoricalDataLoaderStore dataLoaderStore,
        HistoricalAnalyticsWarmupService warmupService,
        ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesAnalyticsHistoricalDataLoaderEventActor.ActorName))
    {
        DataLoader = IsArgumentNull.Set(dataLoader);
        DataLoaderStore = IsArgumentNull.Set(dataLoaderStore);
        WarmupService = IsArgumentNull.Set(warmupService);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public HistoricalDataLoaderService DataLoader { get; }
    /// <inheritdoc />
    public IHistoricalDataLoaderStore DataLoaderStore { get; }
    /// <inheritdoc />
    public HistoricalAnalyticsWarmupService WarmupService { get; }
    /// <inheritdoc />
    public ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> Logger { get; }
}
