using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Telemetry.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryController : CommandControllerBase
    {
        public TelemetryController(ICommandService commandService, ILogger<ReferenceController> logger)
            :base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("AddLogEvents")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> AddLogEventsAsync(AddLogEventsCommand command) 
            => await PostCommandAsync(command);

    }
}