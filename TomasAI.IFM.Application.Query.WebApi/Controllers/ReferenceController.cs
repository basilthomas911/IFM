using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Application.Services;

namespace TomasAI.IFM.Application.Query.WebApi.Controllers
{
    [Produces("application/json")]
    [Route("Reference")]
    public class ReferenceController : BaseQueryController
    {
        private IQueryService _qryService;

        public ReferenceController(IQueryService queryService)
            => _qryService = queryService;

        /// <summary>
        /// return next generated seedId from seed type
        /// </summary>
        /// <param name="seedType"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("SeedId")]
        public async Task<JsonResult> GetNextSeedId([FromQuery] string seedType)
            => await this.GetServiceResultAsync<int>(async () =>
                await _qryService.ExecuteQueryAsync<int>(
                    new GetNextSeedIdQuery { SeedType = seedType }));

        /// <summary>
        /// return selected lookup type
        /// </summary>
        /// <param name="lookupTypeName"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("LookupType/{lookupTypeName}")]
        public async Task<JsonResult> GetLookupType([FromRoute] string lookupTypeName)
            => await this.GetServiceResultAsync<LookupTypeReadModel[]>(async () =>
                await _qryService.ExecuteQueryAsync<LookupTypeReadModel[]>(
                    new GetLookupTypeQuery { LookupTypeName = lookupTypeName }));

    }
}