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

/// <summary>Defines the runtime services required by <see cref="MarketDataFeedQueryActor"/>.</summary>
public interface IMarketDataFeedQueryContext : IQueryActorContext<MarketDataFeedQueryActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<MarketDataFeedQueryActor> Logger { get; }

    /// <summary>Gets the application market-data API.</summary>
    ApplicationMarketDataApi MarketDataApi { get; }
    /// <summary>Gets the sequence-ID generator.</summary>
    ISequenceIdGenerator SequenceIdGenerator { get; }
    IMarketDataServiceStore MarketDataServiceStore { get; }
    IMarketDataLifecycleRequests MarketDataLifecycle { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="MarketDataFeedQueryActor"/>.</summary>
public sealed class MarketDataFeedQueryContext : QueryActorContext, IQueryActorContext<MarketDataFeedQueryActor>, IMarketDataFeedQueryContext
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
