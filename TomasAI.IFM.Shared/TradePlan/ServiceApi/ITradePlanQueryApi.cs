using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradePlan.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Shared.TradePlan.ServiceApi
{
    public interface ITradePlanQueryApi
    {
        Task<ServiceResult<TradePlanStopLossLimitReadModel>> GetIronCondorStopLossLimitAsync(int orderId, int tradeId);
        Task<ServiceResult<TradePlanForwardLossRatioReadModel[]>> GetIronCondorTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate);
        Task<ServiceResult<TradePlanForwardLossRatioReadModel>> GetIronCondorTradePlanForwardLossRatioAsync(DateOnly valueDate);
        Task<ServiceResult<TradePlanReadModel[]>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate);
        Task<ServiceResult<TradePlanReadModel[]>> GetIronCondorTradePlansAsync(int orderId, int tradeId, DateOnly valueDate);
        Task<ServiceResult<IronCondorForwardDeltaDataModel>> GetIronCondorForwardDeltaAsync(string vixContractId, DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType);
        Task<ServiceResult<TradePlanForwardLossLimitReadModel>> GetForwardLossLimitTypeAsync(int OrderId, int TradeId, DateOnly ValueDate, TradeType TradeType);
    }
}
