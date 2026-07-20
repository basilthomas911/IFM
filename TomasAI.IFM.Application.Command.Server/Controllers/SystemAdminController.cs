using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.SystemAdmin.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SystemAdminController : CommandControllerBase
    {

        public SystemAdminController(ICommandService commandService, ILogger<ApplicationController> logger) 
            : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("BackupDatabase")]
        [SwaggerOperation(Summary = "backup database")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> BackupDatabaseAsync(BackupDatabaseCommand command)
            => await PostCommandAsync(command);


    }
}