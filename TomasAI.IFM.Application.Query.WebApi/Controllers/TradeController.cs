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
    [Route("Trade")]
    public class TradeController : BaseQueryController
    {
        private IQueryService _qryService;

        public TradeController(IQueryService queryService)
            => _qryService = queryService;

        /// <summary>
        /// return trade history for selected trade order
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("History/{orderId}")]
        public async Task<JsonResult> GetTradeHistory([FromRoute]int orderId)
            => await this.GetServiceResultAsync<TradeHistoryReadModel[]>(async () =>
                await _qryService.ExecuteQueryAsync<TradeHistoryReadModel[]>(
                    new GetTradeHistoryQuery { OrderId = orderId }));

        [HttpGet]
        [Route("OptionLeg/ContractId/{tradeId}")]
        public async Task<JsonResult> GetOptionLegContractIds([FromRoute]int tradeId)
      => await this.GetServiceResultAsync<string[]>(async () =>
          await _qryService.ExecuteQueryAsync<string[]>(
              new GetOptionLegContractIdsQuery { TradeId = tradeId }));

        [HttpGet]
        [Route("Limit/{tradeId}")]
        public async Task<JsonResult> GetTradeLimit([FromRoute]int tradeId)
      => await this.GetServiceResultAsync<TradeLimitReadModel>(async () =>
          await _qryService.ExecuteQueryAsync<TradeLimitReadModel>(
              new GetTradeLimitQuery { TradeId = tradeId }));
    }
}