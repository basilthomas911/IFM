using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Application.Query.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MarketDataAnalyticsController(IQueryService queryService, ILogger<MarketDataAnalyticsController> logger) : QueryControllerBase(queryService, logger)
{
    protected override string ControllerName => "MarketDataAnalyticsController";

    [HttpGet]
    [Route("FuturesTradeSignal")]
    [SwaggerOperation(Summary = "return futures trade signal")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesTradeSignalV2ReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesTradeSignalQuery(contractId, valueDate));

    [HttpGet]
    [Route("LastFuturesTradeSignal")]
    [SwaggerOperation(Summary = "return last futures trade signal")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesTradeSignalV2ReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetLastFuturesTradeSignalAsync()
        => await ExecuteQueryAsync(new GetLastFuturesTradeSignalQuery());

    [HttpGet]
    [Route("FuturesTradeSignalBySymbol")]
    [SwaggerOperation(Summary = "return futures trade signal by symbol")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesTradeSignalV2ReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesTradeSignalBySymbolQuery(symbol, valueDate));

    [HttpGet]
    [Route("FuturesTradeSignalIds")]
    [SwaggerOperation(Summary = "return futures trade signal ids")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesTradeSignalId[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesTradeSignalIdsQuery (valueDate));

    [HttpGet]
    [Route("FuturesRsiSignal")]
    [SwaggerOperation(Summary = "return futures rsi signal")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesRsiSignalReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateOnly valueDate, FuturesRsiSignalType signalType)
        => await ExecuteQueryAsync(new GetFuturesRsiSignalQuery (contractId, valueDate, signalType));

    [HttpGet]   
    [Route("FuturesTdiSignal")]
    [SwaggerOperation(Summary = "return futures tdi signal")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesTdiSignalReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesTdiSignalQuery (contractId, valueDate));

    [HttpGet]
    [Route("FuturesItiSignal")]
    [SwaggerOperation(Summary = "return futures iti signal")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesItiSignalV2ReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesItiSignalQuery(contractId, valueDate));

    [HttpGet]
    [Route("FuturesItiTrendDirectionChangedSignals")]
    [SwaggerOperation(Summary = "return futures iti trend direction changed signals")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesItiSignalV2ReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesItiSignalV2ReadModel[]>> GetFuturesItiSignalsAsync(string contractId, DateOnly valueDate)
    => await ExecuteQueryAsync(new GetFuturesItiTrendDirectionChangedSignalsQuery(contractId, valueDate));

    [HttpGet]
    [Route("FuturesItiSignalData")]
    [SwaggerOperation(Summary = "return futures iti signal data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesItiSignalDataReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesItiSignalDataQuery (contractId, valueDate));

    [HttpGet]
    [Route("FuturesTrendDirectionFromRSISignal")]
    [SwaggerOperation(Summary = "return futures trend direction from rsi signal")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesTrendDirectionReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateOnly valueDate, DateTime timestamp, int lookBackInterval, DateTime startTime, DateTime endTime)
        => await ExecuteQueryAsync(new GetFuturesTrendDirectionFromRSISignalQuery (contractId, valueDate, timestamp, lookBackInterval, startTime, endTime));

    [HttpGet]
    [Route("FuturesItiSignalMDI")]
    [SwaggerOperation(Summary = "return futures iti signal mdi")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesItiSignalMDIV2ReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesItiSignalMDIQuery(contractId, valueDate));

    [HttpGet]
    [Route("FuturesItiSignalMDIByTrend")]
    [SwaggerOperation(Summary = "return futures iti signal mdi by trend")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesItiSignalMDIV2ReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesItiSignalMDIV2ReadModel[]>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, int groupId)
       => await ExecuteQueryAsync(new GetFuturesItiSignalMDIByTrendQuery(contractId, valueDate, groupId));

    [HttpGet]
    [Route("FuturesItiMDIDistributionByTrend")]
    [SwaggerOperation(Summary = "return futures iti mdi distribution by trend")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesItiMDIDistributionReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesItiMDIDistributionByTrendQuery(contractId, valueDate));

}