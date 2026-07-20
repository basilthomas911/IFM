using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Application.Services;


namespace TomasAI.IFM.Application.Query.WebApi.Controllers
{
    [Produces("application/json")]
    [Route("SpreadTrade")]
    public class SpreadTradeController : BaseQueryController
    {
        private IQueryService _qryService;

        public SpreadTradeController(IQueryService queryService)
            => _qryService = queryService;

        /// <summary>
        /// return selected spread trade
        /// </summary>
        /// <param name="orderId">order id</param>
        /// <param name="tradeId">trade id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("{tradeId}/{orderId}")]
        public async Task<JsonResult> GetSpreadTrade([FromRoute]int tradeId, [FromRoute]int orderId)
            => await this.GetServiceResultAsync<SpreadTradeViewModel>(async () =>
                await _qryService.ExecuteQueryAsync<SpreadTradeViewModel>(
                    new GetSpreadTradeQuery { OrderId = orderId, TradeId = tradeId }));

        /// <summary>
        /// return all spread trade data for trade id
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("Data/{tradeId}")]
        public async Task<JsonResult> GetSpreadTradeDataByTradeId([FromRoute]int tradeId)
                 => await this.GetServiceResultAsync<SpreadTradeDataViewModel[]>(async () =>
                     await _qryService.ExecuteQueryAsync<SpreadTradeDataViewModel[]>(
                         new GetSpreadTradeDataByTradeIdQuery { TradeId = tradeId }));
        /// <summary>
        /// return single spread trade data for trade id
        /// </summary>
        /// <param name="tradeId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("Data/{tradeId}/{tradeType}/{valueDate}/{daysToExpiry}/{tradeStatus}")]
        public async Task<JsonResult> GetSpreadTradeData(
            [FromRoute]int tradeId,
            [FromRoute]TradeType tradeType,
            [FromRoute]DateTime valueDate,
            [FromRoute]int daysToExpiry,
            [FromRoute]TradeStatus tradeStatus)
                 => await this.GetServiceResultAsync<SpreadTradeDataViewModel>(async () =>
                     await _qryService.ExecuteQueryAsync<SpreadTradeDataViewModel>(
                         new GetSpreadTradeDataQuery {
                             TradeId = tradeId,
                             TradeType = tradeType,
                             ValueDate = valueDate,
                             DaysToExpiry = daysToExpiry,
                             TradeStatus = tradeStatus
                         }));

    }
}