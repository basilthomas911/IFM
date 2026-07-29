using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IFuturesItiSignalUIEventConsumer
{
    ValueTask StartAsync(
        Action<FuturesItiSignalV2ReadModel> trendDirectionChangedAction,
        Action<FuturesItiSignalV2ReadModel> trendExtremeChangedAction,
       Action<FuturesTradeSignalV2ReadModel> futuresTradeSignalAction);
    ValueTask StopAsync();
}


