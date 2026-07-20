using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;


namespace TomasAI.IFM.Application.Query.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SystemAdminController(IQueryService queryService, ILogger<SystemAdminController> logger) : QueryControllerBase(queryService, logger)
{
    protected override string ControllerName => "SystemAdminController";

    [HttpGet]
    [Route("DatabaseNames")]
    [SwaggerOperation(Summary = "return database names")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<DatabaseNamesReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<DatabaseNamesReadModel>> ExecuteAsync()
        => await ExecuteQueryAsync(new GetDatabaseNamesQuery());
}