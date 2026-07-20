using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Queries;

namespace TomasAI.IFM.Application.Query.SignalRClient.Queries
{
    public class TradePlanQueryApi : ITradePlanQueryApi
    {
        private IQueryService _querySvc;

        public TradePlanQueryApi(IQueryService querySvc)
        {
            _querySvc = querySvc;
        }

        /// <summary>
        /// return 
        /// </summary>
        /// <param name="lossProbability"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<LossProbabilityViewModel>> GetLossProbabilityAsync(double lossProbability, DateTime valueDate)
            => await _querySvc.ExecuteQueryAsync<LossProbabilityViewModel>(new GetLossProbabilityQuery { ForwardLossRatio = lossProbability, ValueDate = valueDate });

        /// <summary>
        /// return loss probability distribution from value date
        /// </summary>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<LossProbabilityDistributionViewModel>> GetLossProbabilityDistributionAsync(DateTime valueDate)
            => await _querySvc.ExecuteQueryAsync<LossProbabilityDistributionViewModel>(new GetLossProbabilityDistributionQuery { ValueDate = valueDate });

        /// <summary>
        /// return last trade plan stop loss limit
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<TradePlanStopLossLimitReadModel>> GetStopLossLimitAsync(int orderId, int tradeId)
            => await _querySvc.ExecuteQueryAsync<TradePlanStopLossLimitReadModel>(new GetStopLossLimitQuery {OrderId = orderId, TradeId = tradeId});

        /// <summary>
        /// return range of forward loss ratios
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePlanForwardLossRatioReadModel[]>> GetTradePlanForwardLossRatiosAsync(DateTime startDate, DateTime endDate)
            => await _querySvc.ExecuteQueryAsync<TradePlanForwardLossRatioReadModel[]>(new GetTradePlanForwardLossRatiosQuery { StartDate = startDate, EndDate = endDate });
    }

}
