using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Queries;
using TomasAI.IFM.Shared.Fund.ViewModels;

namespace TomasAI.IFM.Application.Query.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FundController(IQueryService queryService, ILogger<FundController> logger)
    : QueryControllerBase(queryService, logger)
{
    protected override string ControllerName => "FundController";

    [HttpGet]
    [Route("Funds")]
    [SwaggerOperation(Summary = "return all available funds")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundReadModel[]>> GetFundsAsync() 
        => await ExecuteQueryAsync(new GetFundsQuery());

    [HttpGet]
    [Route("FundOrders")]
    [SwaggerOperation(Summary = "return fund orders")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundOrderReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync() 
        => await ExecuteQueryAsync(new GetFundOrdersQuery());

    [HttpGet]
    [Route("FundOrderTrades")]
    [SwaggerOperation(Summary = "return fund order trades")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundOrderTradeReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync()
        => await ExecuteQueryAsync(new GetFundOrderTradesQuery());

    [HttpGet]
    [Route("FundTransactions")]
    [SwaggerOperation(Summary = "return fund transactions for selected fund by date range")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundTransactionReadModel[]>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate) 
        => await ExecuteQueryAsync(new GetFundTransactionsQuery (fundId, startDate, endDate));

    [HttpGet]
    [Route("FundBalance")]
    [SwaggerOperation(Summary = "return fund balance for selected fund")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundBalanceReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId) 
        => await ExecuteQueryAsync(new GetFundBalanceQuery(fundId));

    [HttpGet]
    [Route("OpeningFundBalance")]
    [SwaggerOperation(Summary = "return opening fund balance for selected fund")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundBalanceReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate) 
        => await ExecuteQueryAsync(new GetOpeningFundBalanceQuery (fundId, valueDate));

    [HttpGet]
    [Route("ClosingFundBalance")]
    [SwaggerOperation(Summary = "return closing fund balance for selected fund")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundBalanceReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate) 
        => await ExecuteQueryAsync(new GetClosingFundBalanceQuery(fundId, valueDate));

    [HttpGet]
    [Route("FundPnlReport")]
    [SwaggerOperation(Summary = "return fund pnl report for selected fund")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundPnlReportReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(int fundId, DateOnly startDate, DateOnly endDate) 
        => await ExecuteQueryAsync(new GetFundPnlReportQuery(fundId, startDate, endDate));

    [HttpGet]
    [Route("FundIdFromOrderId")]
    [SwaggerOperation(Summary = "return fund id from order id")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<ScalarReadModel<int>>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<ScalarReadModel<int>>> GetFundIdFromOrderIdAsync(int orderId) 
        => await ExecuteQueryAsync(new GetFundIdFromOrderIdQuery(orderId));

    [HttpGet]
    [Route("FundWinLossRatio")]
    [SwaggerOperation(Summary = "return fund win loss ratio")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundWinLossRatioReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundWinLossRatioReadModel>> GetFundWinLossRatioAsync(int fundId, DateOnly startDate, DateOnly endDate) 
        => await ExecuteQueryAsync(new GetFundWinLossRatioQuery(fundId, startDate, endDate));

    [HttpGet]
    [Route("FundDrawdownBalances")]
    [SwaggerOperation(Summary = "return fund drawdown balances by date range")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<FundDrawdownBalancesReadModel>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<FundDrawdownBalancesReadModel>> GetFundDrawdownBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate) 
        => await ExecuteQueryAsync(new GetFundDrawdownBalancesQuery(fundId, startDate, endDate));

}