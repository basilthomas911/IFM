using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi
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
