using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.TradePlan.ServiceApi
{
    public interface ITradePlanCommandApi
    {
        Task<ServiceResult<Guid>> UpdateTradePlanAsync(TradePlanReadModel tradePlan);
        Task<ServiceResult<Guid>> UpdateIronCondorTradePlanAsync(
            DateOnly valueDate,
            IOptionTradeCollection optionTrades,
            FuturesEodDataV2ReadModel futuresEodData,
            double mScore,
            decimal fundBalance);
        Task<ServiceResult<Guid>> UpdateTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel forwardLossLimit);
        Task<ServiceResult<Guid>> ClearTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId id);
    }
}
