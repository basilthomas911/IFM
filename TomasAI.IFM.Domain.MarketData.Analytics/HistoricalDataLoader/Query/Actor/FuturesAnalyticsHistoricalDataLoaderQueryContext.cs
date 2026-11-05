using TomasAI.IFM.Application.Storage;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Actor;

/// <summary>Provides the closed generic context for the data load diagnostics Query actor.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesAnalyticsHistoricalDataLoaderQueryContext
    : QueryActorContext,
      IFuturesAnalyticsHistoricalDataLoaderQueryContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesAnalyticsHistoricalDataLoaderQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<FuturesAnalyticsHistoricalDataLoaderQueryActor> logger)
        : base(supervisor, new(ActorType.Query, FuturesAnalyticsHistoricalDataLoaderQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public ILogger<FuturesAnalyticsHistoricalDataLoaderQueryActor> Logger { get; }
    /// <inheritdoc />
    public IDbContextFactory DbFactory { get; }
}
