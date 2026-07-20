using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Queries;

namespace TomasAI.IFM.Application.Query.SignalRClient.Queries
{
    public class TradeQueryApi : ITradeQueryApi
    {
        private IQueryService _querySvc;

        public TradeQueryApi(IQueryService querySvc)
        {
            _querySvc = querySvc;
        }

        /// <summary>
        /// return trade history for selected trade order
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId)
            => await _querySvc.ExecuteQueryAsync<TradeHistoryReadModel[]>(new GetTradeHistoryQuery { OrderId = orderId });

        /// <summary>
        /// return option lep contract ids
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId)
            => await _querySvc.ExecuteQueryAsync<string[]>(new GetOptionLegContractIdsQuery { TradeId = tradeId });

        /// <summary>
        /// return trade limit for selected trade
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId)
            => await _querySvc.ExecuteQueryAsync<TradeLimitReadModel>(new GetTradeLimitQuery { TradeId = tradeId });

        /// <summary>
        /// return trade type limit
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType )
            => await _querySvc.ExecuteQueryAsync<TradeTypeLimitReadModel>(new GetTradeTypeLimitQuery { TradeId = tradeId , TradeType = tradeType});

        /// <summary>
        /// return trade quantity
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId)
            => await _querySvc.ExecuteQueryAsync<ScalarReadModel<int>>(new GetTradeQuantityQuery { TradeId = tradeId });

        public async Task<ServiceResult<OptionTradeViewModel>> GetOptionTradeAsync(int orderId, int tradeId)
            => await _querySvc.ExecuteQueryAsync<OptionTradeViewModel>(new GetOptionTradeQuery { OrderId = orderId, TradeId = tradeId });

        public async Task<ServiceResult<OptionTradeViewModel[]>> GetOptionTradesAsync(int orderId)
            => await _querySvc.ExecuteQueryAsync<OptionTradeViewModel[]>(new GetOptionTradesQuery { OrderId = orderId });

        public async Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId)
            => await _querySvc.ExecuteQueryAsync<TradePositionReadModel[]>(new GetTradePositionsQuery { OrderId = orderId, TradeId = tradeId });

        public async Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate, int daysToExpiry, TradeStatus tradeStatus)
            => await _querySvc.ExecuteQueryAsync<TradePositionReadModel>(new GetTradePositionQuery { OrderId = orderId, TradeId = tradeId , TradeType = tradeType, ValueDate = valueDate, DaysToExpiry = daysToExpiry, TradeStatus = tradeStatus});

        public async Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(int tradeId, DateTime valueDate)
            => await _querySvc.ExecuteQueryAsync<TradePriceReadModel>(new GetIronCondorTradePriceQuery { TradeId = tradeId, ValueDate = valueDate });
    }
}
