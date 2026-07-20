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
    public class FuturesBarDataController : CommandControllerBase
    {
        /// <summary>
        /// futures bar data controller constructor
        /// </summary>
        /// <param name="commandService"></param>
        public FuturesBarDataController(ICommandService commandService, ILogger<FuturesBarDataController> logger) 
            : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("StartFuturesBarDataStreaming")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StartFuturesBarDataStreamingAsync(StartFuturesBarDataStreamingCommand comand)
            => await ExecuteCommandAsync(comand);

        [HttpPost]
        [Route("StopFuturesBarDataStreaming")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StopFuturesBarDataStreamingAsync(StopFuturesBarDataStreamingCommand command) 
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("InsertFuturesBarData")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertFuturesBarDataAsync(InsertFuturesBarDataCommand command)
            => await PostCommandAsync(command);

    }
}