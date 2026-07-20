using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MarketDataFeedController : CommandControllerBase
    {

        /// <summary>
        /// market data feed controller constructor
        /// </summary>
        /// <param name="commandService"></param>
        public MarketDataFeedController(ICommandService commandService, ILogger<MarketDataFeedController> logger) 
            : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("StartMarketDataFeed")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StartMarketDataFeedAsync(StartMarketDataFeedCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("StopMarketDataFeed")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StopMarketDataFeedAsync(StopMarketDataFeedCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("ResetMarketDataFeed")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ResetMarketDataFeedAsync(ResetMarketDataFeedCommand command) 
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("InsertFuturesEodData")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertFuturesEodDataAsync(InsertFuturesEodDataCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("InsertVixFuturesEodData")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertVixFuturesEodDataAsync(InsertVixFuturesEodDataCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("InsertFuturesBarData")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertFuturesBarDataAsync(InsertFuturesBarDataCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("DeleteFuturesBarData")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> DeleteFuturesBarDataAsync(DeleteFuturesBarDataCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("InsertFuturesClosingPrice")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertFuturesClosingPriceAsync(InsertFuturesClosingPriceCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("TurnTradeLiveFeedOn")]
        [SwaggerOperation(Summary = "turn trade live feed on")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> TurnTradeLiveFeedOnAsync(TurnTradeLiveFeedOnCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("TurnTradeLiveFeedOff")]
        [SwaggerOperation(Summary = "turn trade live feed off")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> TurnTradeLiveFeedOffAsync(TurnTradeLiveFeedOffCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("AddTradeLiveFeed")]
        [SwaggerOperation(Summary = "add trade live feed")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> AddTradeLiveFeedAsync(AddTradeLiveFeedCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("RemoveTradeLiveFeed")]
        [SwaggerOperation(Summary = "remove trade live feed")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> RemoveTradeLiveFeedAsync(RemoveTradeLiveFeedCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("DeleteStreamingRequestId")]
        [SwaggerOperation(Summary = "delete streaming request id")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> DeleteStreamingRequestIdAsync(DeleteStreamingRequestIdCommand command)
           => await PostCommandAsync(command);
    }
}