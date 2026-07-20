using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Query.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TradePlanController(IQueryService queryService, ILogger<TradePlanController> logger) : QueryControllerBase(queryService, logger)
{
    protected override string ControllerName => "TradePlanController";

    [HttpGet]
    [Route("LossProbability")]
    [SwaggerOperation(Summary = "return loss probability")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<LossProbabilityDataModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<LossProbabilityDataModel>> GetLossProbabilityAsync(double forwardLossRatio, DateOnly valueDate) 
        => await ExecuteQueryAsync(new GetLossProbabilityQuery(forwardLossRatio, valueDate));

    [HttpGet]
    [Route("StopLossLimit")]
    [SwaggerOperation(Summary = "return stop loss limit")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradePlanStopLossLimitReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradePlanStopLossLimitReadModel>> GetStopLossLimitAsync(int orderId, int tradeId) 
        => await ExecuteQueryAsync(new GetStopLossLimitQuery(orderId, tradeId));

    [HttpGet]
    [Route("LossProbabilityDistribution")]
    [SwaggerOperation(Summary = "return loss probability distribution")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<LossProbabilityDistributionDataModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<LossProbabilityDistributionDataModel>> GetLossProbabilityDistributionAsync(DateOnly valueDate) 
        => await ExecuteQueryAsync(new GetLossProbabilityDistributionQuery(valueDate));

    [HttpGet]
    [Route("TradePlanForwardLossRatios")]
    [SwaggerOperation(Summary = "return trade plan forward loss ratios")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradePlanForwardLossRatioReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel[]>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate) 
        => await ExecuteQueryAsync(new GetTradePlanForwardLossRatiosQuery( startDate, endDate));

    [HttpGet]
    [Route("TradePlanForwardLossRatio")]
    [SwaggerOperation(Summary = "return trade plan forward loss ratio")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradePlanForwardLossRatioReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatioAsync(DateOnly valueDate) 
        => await ExecuteQueryAsync(new GetTradePlanForwardLossRatioQuery(valueDate));

    [HttpGet]
    [Route("TradePlanAction")]
    [SwaggerOperation(Summary = "return trade plan action")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradePlanActionReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanActionAsync(int orderId, int tradeId, DateOnly valueDate ) 
        => await ExecuteQueryAsync(new GetTradePlanActionQuery( orderId, tradeId, valueDate));

    [HttpGet]
    [Route("TradePlans")]
    [SwaggerOperation(Summary = "return trade plans")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradePlanReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradePlanReadModel[]>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate) 
        => await ExecuteQueryAsync(new GetTradePlansQuery(orderId, tradeId, valueDate));

    [HttpGet]
    [Route("IronCondorForwardDelta")]
    [SwaggerOperation(Summary = "return iron condor forward delta")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<IronCondorForwardDeltaDataModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<IronCondorForwardDeltaDataModel>> GetIronCondorForwardDeltaAsync(DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType) 
        => await ExecuteQueryAsync(new GetIronCondorForwardDeltaQuery(string.Empty, valueDate, tradeType, riskPositionType));

    [HttpGet]
    [Route("TradePlanForwardLossLimit")]
    [SwaggerOperation(Summary = "return trade plan forward loss limit")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradePlanForwardLossLimitReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradePlanForwardLossLimitReadModel>> GetTradePlanForwardLossLimitAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate) 
        => await ExecuteQueryAsync(new GetTradePlanForwardLossLimitQuery(orderId, tradeId, tradeType, valueDate));

}