using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.Domain.Trade.Shared.AlgoTrader
{
    public interface IAlgoTraderService
    {
        Task UpdateTradePlanAsync(OptionTradeSpreadDistributionStatisticsUpdatedEvent e);
    }
}
