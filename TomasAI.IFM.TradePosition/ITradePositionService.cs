using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Service.TradePosition
{
    public interface ITradePositionService
    {
        Task ExecuteAsync(TradePositionChangedEvent e);
        Task ExecuteAsync(OptionTradeLegDataChangedEvent e);
        Task ExecuteAsync(OptionTradeEndOfDayProcessedEvent e);
        Task ExecuteAsync(OptionTradeDistributionStatisticsChangedEvent e);
        Task ExecuteAsync(OptionTradeOrderPlacedEvent e);
    }
}
