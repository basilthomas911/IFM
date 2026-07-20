using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Query.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MarketDataFeedController(IQueryService queryService, ILogger<MarketDataFeedController> logger) 
    : QueryControllerBase(queryService, logger)
{
    protected override string ControllerName => "MarketDataFeedController";

    [HttpGet]
    [Route("LastFuturesTickData")]
    [SwaggerOperation(Summary = "return last futures tick data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesTickDataV2ReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetLastFuturesTickDataQuery(contractId, valueDate));

    [HttpGet]
    [Route("LastFuturesTickDataByTickDate")]
    [SwaggerOperation(Summary = "return last futures tick data by tick date")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesTickDataV2ReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataByTickDateAsync(string contractId, DateTime tickDate)
        => await ExecuteQueryAsync(new GetLastFuturesTickDataByTickDateQuery(contractId, tickDate));

    [HttpGet]
    [Route("LastFuturesOptionTickData")]
    [SwaggerOperation(Summary = "return last futures option tick data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesOptionTickDataV2ReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesOptionTickDataV2ReadModel>> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetLastFuturesOptionTickDataQuery(contractId, valueDate));

    [HttpGet]
    [Route("FuturesBarData")]
    [SwaggerOperation(Summary = "return futures bar data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesBarDataReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesBarDataReadModel[]>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
        => await ExecuteQueryAsync(new GetFuturesBarDataQuery(contractId, symbol, valueDate, startDate, endDate));

    [HttpGet]
    [Route("LastFuturesBarData")]
    [SwaggerOperation(Summary = "return last futures bar data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesBarDataReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesBarDataReadModel[]>> GeLastFuturesBarDataAsync()
        => await ExecuteQueryAsync(new GetLastFuturesBarDataQuery());

    [HttpGet]
    [Route("FuturesEodData")]
    [SwaggerOperation(Summary = "return futures eod data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesEodDataV2ReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesEodDataQuery(contractId, valueDate));

    [HttpGet]
    [Route("LastFuturesEodData")]
    [SwaggerOperation(Summary = "return last futures eod data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesEodDataV2ReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetLastFuturesEodDataAsync()
        => await ExecuteQueryAsync(new GetLastFuturesEodDataQuery());

    [HttpGet]
    [Route("FuturesEodMovingAverages")]
    [SwaggerOperation(Summary = "return futures eod moving averages")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesEodMovingAveragesViewModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesEodMovingAveragesViewModel>> GetFuturesEodMovingAveragesAsync(string contractId, string symbol, DateOnly valueDate)
       => await ExecuteQueryAsync(new GetFuturesEodMovingAveragesQuery(contractId, symbol, valueDate));

    [HttpGet]
    [Route("FuturesEodDataByDateRange")]
    [SwaggerOperation(Summary = "return futures eod data by date range")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesEodDataV2ReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesEodDataV2ReadModel[]>> GetFuturesEodDataByDateRangeAsync(string contractId, DateOnly startDate, DateOnly endDate)
        => await ExecuteQueryAsync(new GetFuturesEodDataByDateRangeQuery(contractId, startDate, endDate));

    [HttpGet]
    [Route("VixFuturesEodData")]
    [SwaggerOperation(Summary = "return vix futures eod data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<VixFuturesEodDataReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<VixFuturesEodDataReadModel[]>> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetVixFuturesEodDataQuery(contractId, valueDate));

    [HttpGet]
    [Route("LastVixFuturesEodData")]
    [SwaggerOperation(Summary = "return last vix futures eod data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<VixFuturesEodDataReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<VixFuturesEodDataReadModel>> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetLastVixFuturesEodDataQuery(contractId,  valueDate));

    [HttpGet]
    [Route("IronCondorMarketDataFeed")]
    [SwaggerOperation(Summary = "return iron condor market data feed")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<IronCondorMarketDataFeedReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<IronCondorMarketDataFeedReadModel>> GetIronCondorMarketDataFeedAsync(string underlyingContractId, string shortPutOptionContractId, 
        string longPutOptionContractId, string shortCallOptionContractId, string longCallOptionContractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetIronCondorMarketDataFeedQuery(underlyingContractId, shortPutOptionContractId, 
            longPutOptionContractId, shortCallOptionContractId, longCallOptionContractId, valueDate));

    [HttpGet]
    [Route("FuturesEodDataParameters")]
    [SwaggerOperation(Summary = "return futures eod data parameters")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesEodDataParametersReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(string contractId, DateOnly valueDate)
        => await ExecuteQueryAsync(new GetFuturesEodDataParametersQuery(contractId, valueDate) );

    [HttpPost]
    [Route("FuturesOptionContract")]
    [SwaggerOperation(Summary = "return futures option contract")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesOptionContractReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(GetFuturesOptionContractQuery query)
        => await ExecuteQueryAsync(query);

    [HttpPost]
    [Route("FuturesOptionSpreadData")]
    [SwaggerOperation(Summary = "return futures option spread data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FuturesOptionSpreadDataReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FuturesOptionSpreadDataReadModel>> GetFuturesOptionSpreadDataAsync(GetFuturesOptionSpreadDataQuery query)
        => await ExecuteQueryAsync(query);

    [HttpGet]
    [Route("NormalCurveTable")]
    [SwaggerOperation(Summary = "return futures bar data")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<NormalCurveTableReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<NormalCurveTableReadModel>> GetNormalCurveTableAsync(GetNormalCurveTableQuery query)
        => await ExecuteQueryAsync(query);

    [HttpGet]
    [Route("FuturesRiskPositionType")]
    [SwaggerOperation(Summary = "return futures risk position type")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<RiskPositionTypeReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<RiskPositionTypeReadModel>> GetFuturesRiskPositionTypeAsync(DateOnly valueDate, TradeType tradeType)
        => await ExecuteQueryAsync(new GetFuturesRiskPositionTypeQuery(valueDate, tradeType));

    [HttpGet]
    [Route("StreamingRequestId")]
    [SwaggerOperation(Summary = "return streaming request id")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarValue<int>>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<ScalarValue<int>>> GetStreamingRequestIdAsync(GetStreamingRequestIdQuery query)
        => await ExecuteQueryAsync(query);

    [HttpGet]
    [Route("OptionQuoteId")]
    [SwaggerOperation(Summary = "return option quote id")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarValue<int>>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<ScalarValue<int>>> GetOptionQuoteIdAsync(GetOptionQuoteIdQuery query)
        => await ExecuteQueryAsync(query);


}