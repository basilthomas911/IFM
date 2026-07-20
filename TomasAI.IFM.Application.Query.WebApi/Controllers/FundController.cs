using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.Queries;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Application.Services;

namespace TomasAI.IFM.Application.Query.WebApi.Controllers
{
    [Produces("application/json")]
    [Route("Fund")]
    public class FundController : BaseQueryController
    {
        private IQueryService _qryService;

        public FundController(IQueryService queryService)
            => _qryService = queryService;

        /// <summary>
        /// return all funds
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("")]
        public async Task<ActionResult> GetFunds()
            => await this.GetServiceResultAsync<FundReadModel[]>(async () =>
                   await _qryService.ExecuteQueryAsync<FundReadModel[]>(new GetFundsQuery { }));

        /// <summary>
        /// return current balance for selected fund
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("Balance/{fundId}")]
        public async Task<ActionResult> GetCurrentFundBalance([FromRoute]int fundId)
            => await this.GetServiceResultAsync<decimal>(async () =>
                   await _qryService.ExecuteQueryAsync<decimal>(
                       new GetFundBalanceQuery { FundId = fundId }));

        /// <summary>
        /// return opening balance for selected fund
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("Balance/Opening/{fundId}/{valueDate}")]
        public async Task<ActionResult> GetOpeningFundBalance([FromRoute]int fundId, [FromRoute]DateTime valueDate)
            => await this.GetServiceResultAsync<decimal>(async () =>
                   await _qryService.ExecuteQueryAsync<decimal>(
                       new GetOpeningFundBalanceQuery {FundId = fundId, ValueDate = valueDate }));

        /// <summary>
        /// return closing balance for selected fund
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("Balance/Closing/{fundId}/{valueDate}")]
        public async Task<ActionResult> GetClosingFundBalance([FromRoute]int fundId, [FromRoute]DateTime valueDate)
            => await this.GetServiceResultAsync<decimal>(async () =>
                   await _qryService.ExecuteQueryAsync<decimal>(
                       new GetClosingFundBalanceQuery { FundId = fundId, ValueDate = valueDate }));
    }
}