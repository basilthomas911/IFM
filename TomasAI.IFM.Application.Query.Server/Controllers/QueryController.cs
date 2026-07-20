using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Query.Services;

namespace TomasAI.IFM.Application.Query.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QueryController : QueryControllerBase
    {
        const string ControllerNameRouteTemplate = "{queryName}";

        string _controllerName;

        public QueryController(IQueryService commandService, ILogger<QueryController> logger)
            :base(commandService, logger)
        {
        }

        protected override string ControllerName => "MarketDataFeed";

        [HttpPost]
        //[Route(ControllerNameRouteTemplate)]
        public async Task ExecuteAsync(string queryName) => await ExecuteQueryAsync(); 
    }
}