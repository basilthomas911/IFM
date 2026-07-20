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
    public class FuturesOptionQuoteDataController : CommandControllerBase
    {

        /// <summary>
        /// futures option quote data controller constructor
        /// </summary>
        /// <param name="commandService"></param>
        public FuturesOptionQuoteDataController(ICommandService commandService, ILogger<FuturesOptionQuoteDataController> logger) : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("StartFuturesOptionQuoteDataStreaming")]
        [SwaggerOperation(Summary = "start futures option quote data streaming")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StartFuturesOptionQuoteDataStreamingAsync(StartFuturesOptionQuoteDataStreamingCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("StopFuturesOptionQuoteDataStreaming")]
        [SwaggerOperation(Summary = "stop futures option quote data streaming")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StopFuturesOptionQuoteDataStreamingAsync(StopFuturesOptionQuoteDataStreamingCommand command) 
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("InsertFuturesOptionQuoteData")]
        [SwaggerOperation(Summary = "insert futures option quote data")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertFuturesOptionTickDataAsync(InsertFuturesOptionQuoteDataCommand command)
            => await PostCommandAsync(command);

    }
}