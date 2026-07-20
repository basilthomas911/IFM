using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.ServiceApi
{
    public interface ITradeCommandApi
    {
        Task<ServiceResult<Guid>> SnapshotAsync(
           int orderId,
           int tradeId
        );

        Task<ServiceResult<Guid>> PlaceOrderAsync(
            TradeOrderReadModel tradeTicket,
            OptionTradeReadModel optionTrade);

        Task<ServiceResult<Guid>> OpenOptionTradeAsync(TradeOrderReadModel tradeOrder);
        Task<ServiceResult<Guid>> CloseOptionTradeAsync(TradeOrderReadModel tradeTicket);

        Task<ServiceResult<Guid>> InsertOptionTradeSpreadDataAsync(OptionTradeSpreadsDataModel optionTradeSpreadData);
        Task<ServiceResult<Guid>> InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarsDataModel optionTradeSpreadBarData);
        Task<ServiceResult<Guid>> DeleteOptionTradeSpreadBarDataAsync(OptionTradeEntityId optionTradeId, TradeType tradeType, DateOnly valueDate);

        Task<ServiceResult<Guid>> ChangeOptionLegDataAsync(
            int orderId,
            int tradeId,
            TradeType tradeType,
            DateOnly valueDate,
            TradeStatus tradeStatus,
            decimal assetPrice,
            double riskFreeRate,
            OptionTradeLegDataReadModel optionLegData);

        Task<ServiceResult<Guid>> ChangeDistributionStatisticsAsync(
            int orderId,
            int tradeId,
            TradeType tradeType,
            DateOnly valueDate,
            TradeStatus tradeStatus,
            SpreadDistributionReadModel putSpreadDistribution,
            SpreadDistributionReadModel callSpreadDistribution);

        Task<ServiceResult<Guid>> ProcessEndOfDayAsync(
            int fundId,
            int orderId,
            int tradeId,
            TradeType tradeType,
            DateOnly valueDate,
            TradeStatus tradeStatus,
            decimal openPrice,
            decimal highPrice,
            decimal lowPrice,
            decimal closePrice,
            int volume,
            string reference);
    
        Task<ServiceResult<Guid>> DeleteAsync(int orderid, int tradeId);

        Task<ServiceResult<Guid>> DeleteAsync(int orderid);

        Task<ServiceResult<Guid>> UpdateTradeLimitDailyProfitTargetAsync(int orderId, int tradeId, int tradingDays, int maxTradingDays);
    }
}
