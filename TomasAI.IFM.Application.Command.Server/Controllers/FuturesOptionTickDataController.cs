using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;


namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FuturesOptionTickDataController : CommandControllerBase
    {

        /// <summary>
        /// futures option tick data controller constructor
        /// </summary>
        /// <param name="commandService"></param>
        public FuturesOptionTickDataController(ICommandService commandService, ILogger<FuturesOptionTickDataController> logger) : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("StartFuturesOptionTickDataStreaming")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StartFuturesOptionTickDataStreamingAsync(StartFuturesOptionTickDataStreamingCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("StopFuturesOptionTickDataStreaming")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StopFuturesOptionTickDataStreamingAsync(StopFuturesOptionTickDataStreamingCommand command) 
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("InsertFuturesOptionTickData")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertFuturesOptionTickDataAsync(InsertFuturesOptionTickDataCommand command)
            => await PostCommandAsync(command);

    }
}