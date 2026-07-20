using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Service.TradePosition;

public interface ITradePositionService
{
    Task ExecuteAsync(TradePositionChangedEvent e);
    Task ExecuteAsync(OptionTradeLegDataChangedEvent e);
    Task ExecuteAsync(OptionTradeEndOfDayProcessedEvent e);
    Task ExecuteAsync(OptionTradeSpreadDistributionStatisticsUpdatedEvent e);
    Task ExecuteAsync(OptionTradeOrderPlacedEvent e);
}
