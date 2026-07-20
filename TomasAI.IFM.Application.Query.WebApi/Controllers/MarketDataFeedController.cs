using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Application.Services;

namespace TomasAI.IFM.Application.Query.WebApi.Controllers
{
    [Produces("application/json")]
    [Route("MarketDataFeed")]
    public class MarketDataFeedController : BaseQueryController
    {
        private IQueryService _qryService;

        public MarketDataFeedController(IQueryService queryService)
            => _qryService = queryService;

        /// <summary>
        /// return last inserted futures tick data
        /// </summary>
        /// <param name="contractId">futures contract id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("Futures/TickData/{contractId}")]
        public async Task<ActionResult> GetLastFuturesTickData([FromRoute]string contractId)
           => await this.GetServiceResultAsync<FuturesTickDataViewModel>(async () =>
                await _qryService.ExecuteQueryAsync<FuturesTickDataViewModel>(
                    new GetLastFuturesTickDataQuery { ContractId = contractId }));

        [HttpGet]
        [Route("Futures/EodData/{symbol}/{valueDate}")]
        public async Task<ActionResult> GetFuturesEodData([FromRoute]string symbol, [FromRoute]DateTime valueDate)
          => await this.GetServiceResultAsync<FuturesEodDataViewModel>(async () =>
               await _qryService.ExecuteQueryAsync<FuturesEodDataViewModel>(
                   new GetFuturesEodDataQuery { Symbol = symbol, ValueDate = valueDate }));

        [HttpGet]
        [Route("Futures/EodData/{symbol}")]
        public async Task<ActionResult> GetFuturesEodDataByDateRange([FromRoute]string symbol, [FromQuery]DateTime startDate, [FromQuery]DateTime endDate)
             => await this.GetServiceResultAsync<FuturesEodDataViewModel[]>(async () =>
                  await _qryService.ExecuteQueryAsync<FuturesEodDataViewModel[]>(
                      new GetFuturesEodDataByDateRangeQuery { Symbol = symbol, StartDate = startDate, EndDate = endDate}));

        /// <summary>
        /// return last inserted futures option tick data
        /// </summary>
        /// <param name="contractId">futures option contract id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("FuturesOption/TickData/{contractId}")]
        public async Task<ActionResult> GetLastFuturesOptionTickData([FromRoute]string contractId)
            => await this.GetServiceResultAsync<FuturesOptionTickDataViewModel>(async () =>
                await _qryService.ExecuteQueryAsync<FuturesOptionTickDataViewModel>(
                    new GetLastFuturesOptionTickDataQuery { ContractId = contractId }));

    }
}