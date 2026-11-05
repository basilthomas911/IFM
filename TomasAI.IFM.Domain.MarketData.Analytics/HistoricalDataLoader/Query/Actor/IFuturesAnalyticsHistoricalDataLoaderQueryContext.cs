using TomasAI.IFM.Application.Storage;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Actor;

/// <summary>Defines readonly services required by the data load diagnostics Query actor.</summary>
public interface IFuturesAnalyticsHistoricalDataLoaderQueryContext
    : IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor>
{
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesAnalyticsHistoricalDataLoaderQueryActor> Logger { get; }
    /// <summary>Gets the database that owns committed attempt diagnostics.</summary>
    IDbContextFactory DbFactory { get; }
}
