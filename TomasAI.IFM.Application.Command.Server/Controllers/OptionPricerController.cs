using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OptionPricerController : CommandControllerBase
    {

        public OptionPricerController(ICommandService commandService, ILogger<ApplicationController> logger) 
            : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("InsertSpreadDistribution")]
        [SwaggerOperation(Summary = "insert spread distribution")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertSpreadDistributionAsync(InsertSpreadDistributionCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("SubmitSpreadDistributionJob")]
        [SwaggerOperation(Summary = "submit spread distribution")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> SubmitSpreadDistributionJobAsync(SubmitSpreadDistributionJobCommand command) 
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("CompleteSpreadDistributionJob")]
        [SwaggerOperation(Summary = "complete spread distribution job")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> CompleteSpreadDistributionJobAsync(CompleteSpreadDistributionJobCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("FailSpreadDistributionJob")]
        [SwaggerOperation(Summary = "fail spread distribution job")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> FailSpreadDistributionJobAsync(FailSpreadDistributionJobCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("ClearSpreadDistributionJob")]
        [SwaggerOperation(Summary = "clear spread distribution job")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ClearSpreadDistributionJobAsync(ClearSpreadDistributionJobCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("DeleteSpreadDistributionJobsInProgress")]
        [SwaggerOperation(Summary = "delete spread distribution job in progress")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> DeleteSpreadDistributionJobsInProgressAsync(DeleteSpreadDistributionJobsInProgressCommand command)
            => await PostCommandAsync(command);

    }
}