using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Application.PredictiveModel.Query.Services;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Queries;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Application.PredictiveModel.Server.Controllers
{
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
        [SwaggerResponse(400, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<ScalarValue<double>>> GetPredictedTrendDeltaAsync(GetPredictedTrendDeltaQuery query) 
            => await ExecuteQueryAsync(query);

        [HttpPost]
        [Route("FuturesItiTrendCoastLineCounters")] 
        [SwaggerOperation(Summary = "return uptrend and downtrend coast line counters")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesItiTrendCoastLineCountersReadModel>))]
        [SwaggerResponse(400, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<FuturesItiTrendCoastLineCountersReadModel>> GetFuturesItiTrendCoastLineCountersAsync(GetFuturesItiTrendCoastLineCountersQuery query)
           => await ExecuteQueryAsync(query);
    }
}