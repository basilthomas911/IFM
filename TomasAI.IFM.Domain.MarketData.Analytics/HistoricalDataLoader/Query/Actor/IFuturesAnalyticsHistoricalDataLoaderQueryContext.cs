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
