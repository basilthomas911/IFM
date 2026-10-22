using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="MarketDataQueryActor"/>.</summary>
public sealed class MarketDataQueryContext : QueryActorContext, IMarketDataQueryContext
{
    /// <summary>Initializes the context.</summary>
    public MarketDataQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<MarketDataQueryActor> logger,
        IFuturesMarketSessionAuthority marketSessionAuthority,
        TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi? marketDataApi = null,
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.IOptionChainIvCollector? ivCollector = null)
        : base(supervisor, new ActorMailboxId(ActorType.Query, MarketDataQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
        MarketSessionAuthority = IsArgumentNull.Set(marketSessionAuthority);
        MarketDataApi = marketDataApi;
        IvCollector = ivCollector;
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<MarketDataQueryActor> Logger { get; }
    /// <inheritdoc/>
    public IFuturesMarketSessionAuthority MarketSessionAuthority { get; }
    /// <inheritdoc/>
    public TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi? MarketDataApi { get; }
    public TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.IOptionChainIvCollector? IvCollector { get; }
}
