using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.AlgoTrader.ServiceApi
{
    public interface IAlgoTraderServiceApi
    {
        Task<ServiceResult> UpdateTradePlanAsync(OptionTradeSpreadDistributionStatisticsUpdatedEvent e);
        Task<ServiceResult> TradePlanUpdatedAsync(TradePlanUpdatedEvent e);

    }
}



