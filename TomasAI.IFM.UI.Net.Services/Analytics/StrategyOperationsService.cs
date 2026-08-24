using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Services.Operations;

namespace TomasAI.IFM.UI.Net.Services.Analytics;

/// <summary>
/// Provides the framework-neutral query and notification boundary used by the Strategy Operations view.
/// </summary>
public sealed class StrategyOperationsService(
    IMarketDataAnalyticsQueryApi queryApi,
    IFuturesItiSignalUIEventConsumer eventConsumer)
    : UiServiceBase<StrategyOperationsService>
{
    readonly IMarketDataAnalyticsQueryApi _queryApi = queryApi
        ?? throw new ArgumentNullException(nameof(queryApi));
    readonly IFuturesItiSignalUIEventConsumer _eventConsumer = eventConsumer
        ?? throw new ArgumentNullException(nameof(eventConsumer));

    /// <summary>Gets the latest typed ITI snapshot for a strategy row.</summary>
    public async ValueTask<UiOperationResult<FuturesItiSignalV2ReadModel>> GetLatestFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _queryApi.GetFuturesItiSignalAsync(contractId, valueDate, timePeriod);
        return result.ToUiResult(value => value);
    }

    /// <summary>Starts the typed ITI notification listener for one UI site.</summary>
    public ValueTask StartFuturesItiSignalListenerAsync(
        Guid siteId,
        Action<FuturesItiSignalUpdatedNotifyEvent> eventAction)
        => _eventConsumer.StartAsync(siteId, eventAction);

    /// <summary>Stops the typed ITI notification listener for one UI site.</summary>
    public ValueTask StopFuturesItiSignalListenerAsync(Guid siteId)
        => _eventConsumer.StopAsync(siteId);
}
