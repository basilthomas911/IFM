using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.QueryParameters;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// Provides methods for querying fund-related data, including balances, transactions, orders, and performance
/// metrics.
/// </summary>
/// <remarks>This API serves as a centralized interface for retrieving various types of fund-related
/// information. It includes methods for querying fund balances, transactions, orders, and performance reports over
/// specified time periods.</remarks>
/// <param name="querySvc"></param>
public class FundQueryApi(IQueryServiceApi querySvc) : IFundQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    /// <summary>
    /// Gets the closing fund balance for a specific fund and value date.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The closing fund balance view model.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate)
    {
        var qryParam = new GetClosingFundBalanceParameter(fundId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FundBalanceReadModel>(FundQueryUriPath.GetClosingFundBalance, qryParam, GetClosingFundBalanceQuery.ErrorId);
    }

    /// <summary>
    /// Gets the current fund balance for a specific fund.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <returns>The fund balance view model.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId)
    {
        var qryParam = new GetFundBalanceParameter(fundId);
        return await _querySvc.ExecuteQueryAsync<FundBalanceReadModel>(FundQueryUriPath.GetFundBalance, qryParam, GetFundBalanceQuery.ErrorId);
    }

    /// <summary>
    /// Gets the fund drawdown balances for a specific fund and date range.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>The fund drawdown balances view model.</returns>
    public async Task<ServiceResult<FundDrawdownBalancesReadModel>> GetFundDrawdownBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var qryParam = new GetFundDrawdownBalancesParameter(fundId, startDate, endDate);
        return await _querySvc.ExecuteQueryAsync<FundDrawdownBalancesReadModel>(FundQueryUriPath.GetFundDrawdownBalances, qryParam, GetFundDrawdownBalancesQuery.ErrorId);
    }

    /// <summary>
    /// Gets the fund ID from an order ID.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>The scalar view model containing the fund ID.</returns>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetFundIdFromOrderIdAsync(int orderId)
    {
        var qryParam = new GetFundIdFromOrderIdParameter(orderId);
        return await _querySvc.ExecuteQueryAsync<ScalarReadModel<int>>(FundQueryUriPath.GetFundIdFromOrderId, qryParam, GetFundIdFromOrderIdQuery.ErrorId);
    }

    /// <summary>
    /// Gets all fund orders.
    /// </summary>
    /// <returns>An array of fund order view models.</returns>
    public async Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync()
    {
        var qryParam = new GetFundOrdersParameter();
        return await _querySvc.ExecuteQueryAsync<FundOrderReadModel[]>(FundQueryUriPath.GetFundOrders, qryParam, GetFundOrdersQuery.ErrorId);
    }

    /// <summary>
    /// Gets all fund order trades.
    /// </summary>
    /// <returns>An array of fund order trade view models.</returns>
    public async Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync()
    {
        var qryParam = new GetFundOrderTradesParameter();
        return await _querySvc.ExecuteQueryAsync<FundOrderTradeReadModel[]>(FundQueryUriPath.GetFundOrderTrades, qryParam, GetFundOrderTradesQuery.ErrorId);
    }

    /// <summary>
    /// Gets the fund PnL report for a specific fund and date range.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>The fund PnL report view model.</returns>
    public async Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var qryParam = new GetFundPnlReportParameter(fundId, startDate, endDate);
        return await _querySvc.ExecuteQueryAsync<FundPnlReportReadModel>(FundQueryUriPath.GetFundPnlReport, qryParam, GetFundPnlReportQuery.ErrorId);
    }

    /// <summary>
    /// Gets all funds.
    /// </summary>
    /// <returns>An array of fund view models.</returns>
    public async Task<ServiceResult<FundReadModel[]>> GetFundsAsync()
    {
        var qryParam = new GetFundsParameter();
        return await _querySvc.ExecuteQueryAsync<FundReadModel[]>(FundQueryUriPath.GetFunds, qryParam, GetFundsQuery.ErrorId);
    }

    /// <summary>
    /// Gets all fund transactions for a specific fund and date range.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>An array of fund transaction view models.</returns>
    public async Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var qryParam = new GetFundTransactionsParameter(fundId, startDate, endDate);
        return await _querySvc.ExecuteQueryAsync<FundTransactionReadModel[]>(FundQueryUriPath.GetFundTransactions, qryParam, GetFundTransactionsQuery.ErrorId);
    }

    /// <summary>
    /// Gets the fund win/loss ratio for a specific fund and date range.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>The fund win/loss ratio view model.</returns>
    public async Task<ServiceResult<FundWinLossRatioReadModel>> GetFundWinLossRatioAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var qryParam = new GetFundWinLossRatioParameter(fundId, startDate, endDate);
        return await _querySvc.ExecuteQueryAsync<FundWinLossRatioReadModel>(FundQueryUriPath.GetFundWinLossRatio, qryParam, GetFundWinLossRatioQuery.ErrorId);
    }

    /// <summary>
    /// Gets the opening fund balance for a specific fund and value date.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The opening fund balance view model.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate)
    {
        var qryParam = new GetOpeningFundBalanceParameter(fundId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FundBalanceReadModel>(FundQueryUriPath.GetOpeningFundBalance, qryParam, GetOpeningFundBalanceQuery.ErrorId);
    }
}
