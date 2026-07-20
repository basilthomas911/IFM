using System;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Query.Client
{
    public class TradePlanQueryApi : ITradePlanQueryApi
    {
        readonly IQueryService _querySvc;
        readonly string _controller;

        public TradePlanQueryApi(IQueryService querySvc)
        {
            _querySvc = IsArgumentNull.Set(querySvc);
            _controller = "TradePlan";
        }

        /// <summary>
        /// return 
        /// </summary>
        /// <param name="lossProbability"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<LossProbabilityViewModel>> GetLossProbabilityAsync(double lossProbability, DateOnly valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetLossProbabilityQuery { ForwardLossRatio = lossProbability, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return loss probability distribution from value date
        /// </summary>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<LossProbabilityDistributionViewModel>> GetLossProbabilityDistributionAsync(DateOnly valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetLossProbabilityDistributionQuery { ValueDate = valueDate }, _controller);

        /// <summary>
        /// return last trade plan stop loss limit
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<TradePlanStopLossLimitReadModel>> GetStopLossLimitAsync(int orderId, int tradeId)
            => await _querySvc.ExecuteApiQueryAsync(new GetStopLossLimitQuery {OrderId = orderId, TradeId = tradeId}, _controller);

        /// <summary>
        /// return range of forward loss ratios
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePlanForwardLossRatioReadModel[]>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanForwardLossRatiosQuery { StartDate = startDate, EndDate = endDate }, _controller);

        /// <summary>
        /// return forward loss ratio by value date
        /// </summary>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatioAsync(DateOnly valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanForwardLossRatioQuery { ValueDate = valueDate }, _controller);

        /// <summary>
        /// return trade plan action for selected orderid/tradeid
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanActionAsync(int orderId, int tradeId) 
            => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanActionQuery { OrderId = orderId, TradeId = tradeId }, _controller);

        /// <summary>
        /// return trade planS for selected orderid/tradeid
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePlanReadModel[]>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradePlansQuery { OrderId = orderId, TradeId = tradeId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return iron condor forward delta
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="tradeType"></param>
        /// <returns></returns>
        public async Task<ServiceResult<IronCondorForwardDeltaViewModel>> GetIronCondorForwardDeltaAsync(DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType)
            => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorForwardDeltaQuery { ValueDate = valueDate, TradeType = tradeType, RiskPositionType = riskPositionType}, _controller);

        /// <summary>
        /// return  trade plan forward loss limit
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePlanForwardLossLimitReadModel>> GetTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitId id)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanForwardLossLimitQuery { ForwardLossLimitId = id }, _controller);

    }

}
