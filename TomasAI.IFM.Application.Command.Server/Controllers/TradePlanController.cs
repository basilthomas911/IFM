using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.TradePlan.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TradePlanController : CommandControllerBase
    {

        public TradePlanController(ICommandService commandService, ILogger<ApplicationController> logger) 
            : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("UpdateTradePlan")]
        [SwaggerOperation(Summary = "update trade plan")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> UpdateTradePlanAsync(UpdateTradePlanCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("UpdateIronCondorTradePlan")]
        [SwaggerOperation(Summary = "update iron condor trade plan")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> UpdateIronCondorTradePlanAsync(UpdateIronCondorTradePlanCommand command) 
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("UpdateTradePlanForwardLossLimit")]
        [SwaggerOperation(Summary = "update trade plan forward loss limit")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> UpdateTradePlanForwardLossLimitAsync(UpdateTradePlanForwardLossLimitCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("ClearTradePlanForwardLossLimit")]
        [SwaggerOperation(Summary = "clear trade plan forward loss limit")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ClearTradePlanForwardLossLimitAsync(ClearTradePlanForwardLossLimitCommand command)
            => await PostCommandAsync(command);
    }
}