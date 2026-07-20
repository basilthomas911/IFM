using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Application.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApplicationController : CommandControllerBase
    {

        public ApplicationController(ICommandService commandService, ILogger<ApplicationController> logger) 
            : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("StartApplication")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ExecuteStartApplicationAsync(StartApplicationCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("ShutdownApplication")]
        [SwaggerOperation(Summary = "shutdown application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ExecuteShutdownApplicationAsync(ShutdownApplicationCommand command) 
            => await ExecuteCommandAsync(command);
        
    }
}