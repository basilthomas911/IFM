using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using HistoricalDataLoaderService = TomasAI.IFM.Application.MarketData.Historical.HistoricalDataLoader;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Actor;

/// <summary>Defines readonly services required by the durable data load Event actor.</summary>
public interface IFuturesAnalyticsHistoricalDataLoaderEventContext
    : IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor>
{
    /// <summary>Gets the provider-neutral data load coordinator.</summary>
    HistoricalDataLoaderService DataLoader { get; }
    /// <summary>Gets the durable operational data load store.</summary>
    IHistoricalDataLoaderStore DataLoaderStore { get; }
    /// <summary>Gets the Development-only automatic coverage coordinator.</summary>
    HistoricalAnalyticsWarmupService WarmupService { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> Logger { get; }
}

/// <summary>Provides the closed generic context for the durable data load Event actor.</summary>
public sealed class FuturesAnalyticsHistoricalDataLoaderEventContext
    : EventActorContext,
      IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor>,
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
