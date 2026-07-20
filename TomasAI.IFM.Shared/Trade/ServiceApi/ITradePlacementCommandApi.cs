using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.ServiceApi;

public interface ITradePlacementCommandApi
{
    Task<ServiceResult<Guid>> SignalTradePlacementAsync(FuturesTradeSignalV2ReadModel futuresTradeSignal);
    Task<ServiceResult<Guid>> StartTradePlacementAsync(TradePlacementId tradePlacementId);
    Task<ServiceResult<Guid>> StopTradePlacementAsync(TradePlacementId tradePlacementId);

}
