using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Actor;

/// <summary>Defines readonly services required by the data load diagnostics Query actor.</summary>
public interface IFuturesAnalyticsHistoricalDataLoaderQueryContext
    : IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor>
{
    /// <summary>Gets the durable data load operational store.</summary>
    IHistoricalDataLoaderStore DataLoaderStore { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesAnalyticsHistoricalDataLoaderQueryActor> Logger { get; }
}

/// <summary>Provides the closed generic context for the data load diagnostics Query actor.</summary>
public sealed class FuturesAnalyticsHistoricalDataLoaderQueryContext
    : QueryActorContext,
      IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor>,
      IFuturesAnalyticsHistoricalDataLoaderQueryContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesAnalyticsHistoricalDataLoaderQueryContext(
        IActorSupervisor supervisor,
        IHistoricalDataLoaderStore dataLoaderStore,
        ILogger<FuturesAnalyticsHistoricalDataLoaderQueryActor> logger)
        : base(supervisor, new(ActorType.Query, FuturesAnalyticsHistoricalDataLoaderQueryActor.ActorName))
    {
        DataLoaderStore = IsArgumentNull.Set(dataLoaderStore);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IHistoricalDataLoaderStore DataLoaderStore { get; }
    /// <inheritdoc />
    public ILogger<FuturesAnalyticsHistoricalDataLoaderQueryActor> Logger { get; }
}
