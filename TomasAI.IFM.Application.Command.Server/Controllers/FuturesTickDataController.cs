using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FuturesTickDataController : CommandControllerBase
    {
  
        /// <summary>
        /// futures tick data controller constructor
        /// </summary>
        /// <param name="commandService"></param>
        public FuturesTickDataController(ICommandService commandService, ILogger<FuturesTickDataController> logger) 
            : base(commandService, logger   )
        {
        }

        [HttpPost]
        [Route("StartFuturesTickDataStreaming")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StartFuturesTickDataStreamingAsync(StartFuturesTickDataStreamingCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("StopFuturesTickDataStreaming")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StopFuturesTickDataStreamingAsync(StopFuturesTickDataStreamingCommand command) 
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("InsertFuturesTickData")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertFuturesTickDataAsync(InsertFuturesTickDataCommand command)
            => await PostCommandAsync(command);

    }
}