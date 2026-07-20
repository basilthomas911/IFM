using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Query.Client
{
    public class TradeQueryApi : ITradeQueryApi
    {
        readonly IQueryService _querySvc;
        readonly string _controller;

        public TradeQueryApi(IQueryService querySvc)
        {
            _querySvc = IsArgumentNull.Set(querySvc);
            _controller = "Trade";
        }

        /// <summary>
        /// return trade history for selected trade order
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradeHistoryQuery { OrderId = orderId }, _controller).ConfigureAwait(false);

        /// <summary>
        /// return option lep contract ids
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId)
            => await _querySvc.ExecuteApiQueryAsync(new GetOptionLegContractIdsQuery { TradeId = tradeId }, _controller);

        /// <summary>
        /// return trade limit for selected trade
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradeLimitQuery { TradeId = tradeId }, _controller);

        /// <summary>
        /// return trade type limit
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType )
            => await _querySvc.ExecuteApiQueryAsync(new GetTradeTypeLimitQuery { TradeId = tradeId , TradeType = tradeType}, _controller);

        /// <summary>
        /// return trade quantity
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradeQuantityQuery { TradeId = tradeId }, _controller);

        /// <summary>
        /// return option trade
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<OptionTradeViewModel>> GetOptionTradeAsync(int orderId, int tradeId)
            => await _querySvc.ExecuteApiQueryAsync(new GetOptionTradeQuery { OrderId = orderId, TradeId = tradeId }, _controller);

        /// <summary>
        /// return option trades
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<OptionTradeViewModel[]>> GetOptionTradesAsync(int orderId)
            => await _querySvc.ExecuteApiQueryAsync(new GetOptionTradesQuery { OrderId = orderId }, _controller);

        /// <summary>
        /// return option trade spread data
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<OptionTradeSpreadDataViewModel>> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetOptionTradeSpreadDataQuery { OrderId = orderId, TradeId = tradeId, TradeType = tradeType, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return option trade spread bar data
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="valueDate"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<OptionTradeSpreadBarDataViewModel[]>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate, DateTime startDate, DateTime endDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetOptionTradeSpreadBarDataQuery { OrderId = orderId, TradeId = tradeId, TradeType = tradeType, ValueDate = valueDate, StartDate = startDate, EndDate = endDate }, _controller);

        /// <summary>
        /// return trade positions
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradePositionsQuery { OrderId = orderId, TradeId = tradeId }, _controller);

        /// <summary>
        /// return trade positions by trade type
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="valueDate"></param>
        /// <param name="daysToExpiry"></param>
        /// <param name="tradeStatus"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate, int daysToExpiry, TradeStatus tradeStatus)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradePositionQuery { OrderId = orderId, TradeId = tradeId , TradeType = tradeType, ValueDate = valueDate, DaysToExpiry = daysToExpiry, TradeStatus = tradeStatus}, _controller);

        /// <summary>
        /// return iron condor trade price
        /// </summary>
        /// <param name="tradeId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(int tradeId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorTradePriceQuery { TradeId = tradeId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return trade plan actions
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync(DateTime valueDate, int orderId, int tradeId)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanActionQuery { ValueDate = valueDate, OrderId = orderId, TradeId = tradeId }, _controller);

        /// <summary>
        /// return trade position trade types
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(
            int orderId,
            int tradeId,
             DateTime valueDate,
            int daysToExpiry,
            TradeStatus tradeStatus)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradePositionTradeTypesQuery {
                OrderId = orderId, 
                TradeId = tradeId, 
                ValueDate = valueDate,
                DaysToExpiry = daysToExpiry,
                TradeStatus = tradeStatus
            }, _controller);

        /// <summary>
        /// eturn iron condor market data indicator limit info
        /// </summary>
        /// <param name="optionTradeId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<IronCondorMDILimitViewModel>> GetIronCondorMDILimitAsync(OptionTradeId optionTradeId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorMDILimitQuery { OptionTradeId = optionTradeId, ValueDate = valueDate }, _controller);
    }
}
