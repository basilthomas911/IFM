using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReferenceController(ICommandService commandService, ILogger<ReferenceController> logger) : CommandControllerBase(commandService, logger)
    {
        [HttpPost]
        [Route("ChangeEconomicCalendar")]
        [SwaggerOperation(Summary = "change economic calendar")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ChangeEconomicCalendarAsync(ChangeEconomicCalendarCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("AddEconomicCalendar")]
        [SwaggerOperation(Summary = "add economic calendar")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> AddEconomicCalendarAsync(AddEconomicCalendarCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("RemoveEconomicCalendar")]
        [SwaggerOperation(Summary = "remove economic calendar")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> RemoveEconomicCalendarAsync(RemoveEconomicCalendarCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("ImportEconomicCalendars")]
        [SwaggerOperation(Summary = "import economic calendars")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ImportEconomicCalendarsAsync(ImportEconomicCalendarsCommand command) 
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("AddLookupType")]
        [SwaggerOperation(Summary = "add lookup type")]
        [SwaggerResponse(201, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(400, "bad request")]
        [SwaggerResponse(404, "not found")]
        [SwaggerResponse(409, "conflict")]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> AddLookupTypeAsync(AddLookupTypeCommand command)
            => await ExecuteCommandAsync(command);
        
        [HttpPost]
        [Route("ChangeLookupType")]
        [SwaggerOperation(Summary = "change lookup type")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(400, "bad request")]
        [SwaggerResponse(404, "not found")]
        [SwaggerResponse(409, "conflict")]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ChangeLookupTypeAsync(ChangeLookupTypeCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("RemoveLookupType")]
        [SwaggerOperation(Summary = "remove lookup type")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(400, "bad request")]
        [SwaggerResponse(404, "not found")]
        [SwaggerResponse(409, "conflict")]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> RemoveLookupTypeAsync(RemoveLookupTypeCommand command)
            => await ExecuteCommandAsync(command);


    }
}