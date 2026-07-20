using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Application.Services;


namespace TomasAI.IFM.Application.Query.WebApi.Controllers
{
    [Produces("application/json")]
    [Route("MarketData")]
    public class MarketDataController : BaseQueryController
    {
        private IQueryService _qryService;

        public MarketDataController(IQueryService queryService)
        {
            _qryService = queryService;
        }

        /// <summary>
        /// return selected futures contract
        /// </summary>
        /// <param name="contractId">futures contract id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("Futures/Contract/{contractId}")]
        public async Task<ActionResult> GetFuturesContract([FromRoute]string contractId)
            => await this.GetServiceResultAsync<FuturesContractViewModel>( async () =>
                    await _qryService.ExecuteQueryAsync<FuturesContractViewModel>(
                        new GetFuturesContractQuery { ContractId = contractId }));

        /// <summary>
        /// return all futures contract by symbol
        /// </summary>
        /// <param name="symbol">futures symbol</param>
        /// <returns></returns>
        [HttpGet]
        [Route("Futures/Contract")]
        public async Task<ActionResult> GetFuturesContracts([FromQuery]string symbol)
            => await this.GetServiceResultAsync<FuturesContractViewModel[]>(async () =>
                   await _qryService.ExecuteQueryAsync<FuturesContractViewModel[]>(
                       new GetFuturesContractsQuery { Symbol = symbol }));


        /// <summary>
        /// return selected futures option contract
        /// </summary>
        /// <param name="contractId">futures contract id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("FuturesOption/Contract/{contractId}")]
        public async Task<ActionResult> GetFuturesOptionContract([FromRoute] string contractId)
           => await this.GetServiceResultAsync<FuturesOptionContractReadModel>(async () =>
                await _qryService.ExecuteQueryAsync<FuturesOptionContractReadModel>(
                    new GetFuturesOptionContractQuery { ContractId = contractId }));

        /// <summary>
        /// return selected futures option contracts
        /// </summary>
        /// <param name="symbol">futures symbol</param>
        /// <returns></returns>
        [HttpGet]
        [Route("FuturesOption/Contract")]
        public async Task<ActionResult> GetFuturesOptionContracts([FromQuery] string symbol)
           => await this.GetServiceResultAsync<FuturesOptionContractReadModel[]>(async () =>
                await _qryService.ExecuteQueryAsync<FuturesOptionContractReadModel[]>(
                    new GetFuturesOptionContractsQuery { Symbol = symbol }));

        /// <summary>
        /// return last updated yield curve rate
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("YieldCurveRate")]
        public async Task<ActionResult> GetLastYieldCurveRate()
           => await this.GetServiceResultAsync<YieldCurveRateReadModel>(async () =>
                await _qryService.ExecuteQueryAsync<YieldCurveRateReadModel>(
                    new GetLastYieldCurveRateQuery { }));

        /// <summary>
        /// return last updated rate of return
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("RateOfReturn")]
        public async Task<ActionResult> GetLastRateOfReturn([FromQuery]string symbol)
           => await this.GetServiceResultAsync<ReturnRateViewModel>(async () =>
                await _qryService.ExecuteQueryAsync<ReturnRateViewModel>(
                    new GetLastRateOfReturnQuery { Symbol = symbol }));

        /// <summary>
        /// return trading days
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("TradingDays")]
        public async Task<ActionResult> GetTradingDays(
            [FromQuery]DateTime startDate,
            [FromQuery]DateTime endDate,
            [FromQuery]MarketType marketType,
            [FromQuery]CurrencyType currencyType)
         => await this.GetServiceResultAsync<int>(async () =>
                await _qryService.ExecuteQueryAsync<int>(
                    new GetTradingDaysQuery
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        MarketType = marketType,
                        CurrencyType = currencyType
                    }));

    }
}