using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MarketDataAnalyticsController : CommandControllerBase
    {
        /// <summary>
        /// market data analytics controller constructor
        /// </summary>
        /// <param name="commandService"></param>
        public MarketDataAnalyticsController(ICommandService commandService, ILogger<MarketDataAnalyticsController> logger) 
            : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("StartFuturesRsiSignal")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StartFuturesRsiSignalAsync(StartFuturesRsiSignalCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("StopFuturesRsiSignal")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StopFuturesRsiSignalAsync(StopFuturesRsiSignalCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("StartFuturesTradeSignalLLM")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StartFuturesTradeSignalLLMsync(StartFuturesTradeSignalLLMCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("StopFuturesTradeSignalLLM")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> StopFuturesTradeSignalLLMAsync(StopFuturesTradeSignalLLMCommand command) 
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("UpdateFuturesTradeSignal")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> UpdateFuturesTradeSignalAsync(UpdateFuturesTradeSignalCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("GenerateFuturesRsiSignal")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> GenerateFuturesRsiSignalAsync(GenerateFuturesRsiSignalCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("GenerateFuturesTdiSignal")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> GenerateFuturesTdiSignalAsync(GenerateFuturesTdiSignalCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("GenerateFuturesTradeSignalLLM")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> GenerateFuturesTradeSignalLLMAsync(GenerateFuturesTradeSignalLLMCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("GenerateFuturesItiSignal")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> GenerateFuturesItiSignalAsync(GenerateFuturesItiSignalCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("SetFuturesItiSignalHoldTrade")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> SetFuturesItiSignalHoldTradeAsync(SetFuturesItiSignalHoldTradeCommand command)
            => await PostCommandAsync(command   );

        [HttpPost]
        [Route("ClearFuturesItiSignalHoldTrade")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ClearFuturesItiSignalHoldTradeAsync(ClearFuturesItiSignalHoldTradeCommand command)
            => await PostCommandAsync(command);
    }
}