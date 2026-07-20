using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Queries;

namespace TomasAI.IFM.Application.Query.Server.Controllers.PredictiveModel;

[Route("api/[controller]")]
[ApiController]
public class FuturesItiTrendController(IQueryService queryService, ILogger<FuturesItiTrendController> logger)
    : QueryControllerBase(queryService, logger)
{
    protected override string ControllerName => "FuturesItiTrendController";

    [HttpPost]
    [Route("PredictedTrendDelta")]
    [SwaggerOperation(Summary = "return predicted trend delta")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarValue<double>>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<ScalarValue<double>>> GetPredictedTrendDeltaAsync(GetPredictedTrendDeltaQuery query) 
        => await ExecuteQueryAsync(query);

}