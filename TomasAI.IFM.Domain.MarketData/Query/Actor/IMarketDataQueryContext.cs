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

/// <summary>Defines the runtime services required by <see cref="MarketDataQueryActor"/>.</summary>
public interface IMarketDataQueryContext : IQueryActorContext<MarketDataQueryActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<MarketDataQueryActor> Logger { get; }
    /// <summary>Gets the API process's authoritative futures-session state.</summary>
    IFuturesMarketSessionAuthority MarketSessionAuthority { get; }
    /// <summary>Gets provider-backed market metadata.</summary>
    TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi? MarketDataApi { get; }
    TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility.IOptionChainIvCollector? IvCollector => null;
}
