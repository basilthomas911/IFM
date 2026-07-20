using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.Queries;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Query.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OptionPricerController(IQueryService queryService, ILogger<OptionPricerController> logger) 
    : QueryControllerBase(queryService, logger)
{
    protected override string ControllerName => "OptionPricerController";

    [HttpGet]
    [Route("OptionPricerDevices")]
    [SwaggerOperation(Summary = "return option pricer devices")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<OptionPricerDevicesReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync()
        => await ExecuteQueryAsync(new GetOptionPricerDevicesQuery ());

    [HttpGet]
    [Route("SpreadDistribution")]
    [SwaggerOperation(Summary = "return spread distribution")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<SpreadDistributionReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
        => await ExecuteQueryAsync(new GetSpreadDistributionQuery(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry));

    [HttpGet]
    [Route("SpreadDistributionJobInProgress")]
    [SwaggerOperation(Summary = "return spread distribution in progress")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarReadModel<bool>>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<ScalarReadModel<bool>>> GetSpreadDistributionJobInProgressAsync(int orderId, int tradeId)
        => await ExecuteQueryAsync(new GetSpreadDistributionJobInProgressQuery(orderId, tradeId));
}