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
