using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

public interface ITradePlacementCommandApi
{
    Task<ServiceResult<Guid>> SignalTradePlacementAsync(FuturesTradeSignalV2ReadModel futuresTradeSignal);
    Task<ServiceResult<Guid>> StartTradePlacementAsync(
        TradePlacementId tradePlacementId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> StopTradePlacementAsync(
        TradePlacementId tradePlacementId,
        CancellationToken cancellationToken = default);

}
