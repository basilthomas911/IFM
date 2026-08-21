using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Models.Operations;

/// <summary>
/// Provides the framework-neutral query and notification boundary used by the Strategy Operations view.
/// </summary>
public sealed class StrategyOperationsModel(
    IMarketDataAnalyticsQueryApi queryApi,
    IFuturesItiSignalUIEventConsumer eventConsumer)
    : BaseModel<StrategyOperationsModel>
{
    readonly IMarketDataAnalyticsQueryApi _queryApi = queryApi
        ?? throw new ArgumentNullException(nameof(queryApi));
    readonly IFuturesItiSignalUIEventConsumer _eventConsumer = eventConsumer
        ?? throw new ArgumentNullException(nameof(eventConsumer));

    public Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetLatestFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod)
        => _queryApi.GetFuturesItiSignalAsync(contractId, valueDate, timePeriod);

    public ValueTask StartFuturesItiSignalListenerAsync(
        Guid siteId,
        Action<FuturesItiSignalUpdatedNotifyEvent> eventAction)
        => _eventConsumer.StartAsync(siteId, eventAction);

    public ValueTask StopFuturesItiSignalListenerAsync(Guid siteId)
        => _eventConsumer.StopAsync(siteId);
}
