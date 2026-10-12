using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="MarketDataFeedQueryActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class MarketDataFeedQueryContext : QueryActorContext, IMarketDataFeedQueryContext
{
    /// <summary>Initializes the typed query context.</summary>
    public MarketDataFeedQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<MarketDataFeedQueryActor> logger,
        ApplicationMarketDataApi marketDataApi,
        ISequenceIdGenerator sequenceIdGenerator,
        IMarketDataServiceStore marketDataServiceStore,
        IMarketDataLifecycleRequests marketDataLifecycle)
        : base(supervisor, new ActorMailboxId(ActorType.Query, MarketDataFeedQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        SequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
        MarketDataServiceStore = IsArgumentNull.Set(marketDataServiceStore);
        MarketDataLifecycle = IsArgumentNull.Set(marketDataLifecycle);
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<MarketDataFeedQueryActor> Logger { get; }

    /// <inheritdoc/>
    public ApplicationMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public ISequenceIdGenerator SequenceIdGenerator { get; }
    public IMarketDataServiceStore MarketDataServiceStore { get; }
    public IMarketDataLifecycleRequests MarketDataLifecycle { get; }
}
