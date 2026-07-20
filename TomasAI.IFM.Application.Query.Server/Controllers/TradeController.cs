using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Query.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TradeController(IQueryService queryService, ILogger<TradeController> logger) : QueryControllerBase(queryService, logger)
{
    protected override string ControllerName => "TradeController";

    [HttpGet]
    [Route("OptionTrade")]
    [SwaggerOperation(Summary = "return option trade")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<OptionTradeDataModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<OptionTradeDataModel>> GetOptionTradeAsync(int orderId, int tradeId)
        => await ExecuteQueryAsync(new GetOptionTradeQuery(orderId, tradeId));

    [HttpGet]
    [Route("OptionTrades")]
    [SwaggerOperation(Summary = "return all option trades from order id")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<OptionTradeDataModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<OptionTradeDataModel[]>> GetOptionTradesAsync(int orderId)
        => await ExecuteQueryAsync(new GetOptionTradesQuery(orderId));

    [HttpGet]
    [Route("TradePositions")]  
    [SwaggerOperation(Summary = "return option trade positions")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradePositionReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId)
        => await ExecuteQueryAsync(new GetTradePositionsQuery(orderId, tradeId));

    [HttpGet]
    [Route("TradePosition")]
    [SwaggerOperation(Summary = "return option trade position")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradePositionReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
        => await ExecuteQueryAsync(new GetTradePositionQuery(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus));

    [HttpGet]
    [Route("TradeHistory")]
    [SwaggerOperation(Summary = "return option trade position")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradeHistoryReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId)
        => await ExecuteQueryAsync(new GetTradeHistoryQuery(orderId));

    [HttpGet]
    [Route("OptionLegContractIds")]
    [SwaggerOperation(Summary = "return option leg contract ids")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<string[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId)
        => await ExecuteQueryAsync(new GetOptionLegContractIdsQuery(tradeId));

    [HttpGet]
    [Route("TradeLimit")]
    [SwaggerOperation(Summary = "return trade limit")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradeLimitReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId)
        => await ExecuteQueryAsync(new GetTradeLimitQuery(tradeId));

    [HttpGet]
    [Route("TradeTypeLimit")]
    [SwaggerOperation(Summary = "return trade type limit")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradeTypeLimitReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType)
        => await ExecuteQueryAsync(new GetTradeTypeLimitQuery(tradeId, tradeType));

    [HttpGet]
    [Route("TradeQuantity")]
    [SwaggerOperation(Summary = "return trade type limit")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarReadModel<int>>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId)
        => await ExecuteQueryAsync(new GetTradeQuantityQuery(tradeId));

    [HttpGet]
    [Route("IronCondorTradePrice")]
    [SwaggerOperation(Summary = "return iron condor trade price")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<TradePriceReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetIronCondorTradePriceQuery(tradeId, valueDate));

    [HttpGet]
    [Route("TradePositionTradeTypes")]
    [SwaggerOperation(Summary = "return trade position trade type")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<string[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
        => await ExecuteQueryAsync(new GetTradePositionTradeTypesQuery(orderId, tradeId, valueDate, daysToExpiry, tradeStatus));

    [HttpGet]
    [Route("OptionTradeSpreadData")]
    [SwaggerOperation(Summary = "return option trade spread data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<OptionTradeSpreadsDataModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetOptionTradeSpreadDataQuery(orderId, tradeId, tradeType, valueDate));

    [HttpGet]
    [Route("OptionTradeSpreadBarData")]
    [SwaggerOperation(Summary = "return option trade spread bar data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<OptionTradeSpreadBarsDataModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, DateTime startDate, DateTime endDate)
        => await ExecuteQueryAsync(new GetOptionTradeSpreadBarDataQuery(orderId, tradeId, tradeType, valueDate, startDate, endDate));

    [HttpGet]
    [Route("IronCondorMDILimit")]
    [SwaggerOperation(Summary = "return iron condor mdi limit")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<IronCondorMDILimitDataModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<IronCondorMDILimitDataModel>> ExecuteAsync(int orderId, int tradeId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetIronCondorMDILimitQuery(orderId, tradeId, valueDate));
}