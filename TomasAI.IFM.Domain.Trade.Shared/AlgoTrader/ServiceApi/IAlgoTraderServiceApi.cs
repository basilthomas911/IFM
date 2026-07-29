using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.AlgoTrader.ServiceApi
{
    public interface IAlgoTraderServiceApi
    {
        Task<ServiceResult> UpdateTradePlanAsync(OptionTradeSpreadDistributionStatisticsUpdatedEvent e);
        Task<ServiceResult> TradePlanUpdatedAsync(TradePlanUpdatedEvent e);

    }
}



