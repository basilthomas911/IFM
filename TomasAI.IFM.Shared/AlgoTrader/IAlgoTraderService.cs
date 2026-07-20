using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Shared.AlgoTrader
{
    public interface IAlgoTraderService
    {
        Task UpdateTradePlanAsync(OptionTradeSpreadDistributionStatisticsUpdatedEvent e);
    }
}
