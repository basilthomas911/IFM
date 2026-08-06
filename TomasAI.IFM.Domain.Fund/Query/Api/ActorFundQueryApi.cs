using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Query.Api;

/// <summary>
/// Provides direct, in-process access to Fund domain queries without actor messaging.
/// </summary>
/// <remarks>
/// The implementation uses <see cref="IDbContextFactory"/> for Fund storage access. Every public query
/// returns a typed <see cref="ServiceOk{T}"/> or a query-specific <see cref="ServiceFailed{T}"/>.
/// Instances are safe to register as application singletons because they do not capture actor context.
/// </remarks>
public sealed class ActorFundQueryApi(IDbContextFactory dbFactory) : IActorFundQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets funds.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundReadModel[]>> GetFundsAsync()
    {
        try
        {
            FundReadModel[] result = [.. await _dbFactory.FundDb.GetFundsAsync()];
            return new ServiceOk<FundReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundReadModel[]>(GetFundsQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund orders.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync()
    {
        try
        {
            FundOrderReadModel[] result = [.. await _dbFactory.FundDb.GetFundOrdersAsync()];
            return new ServiceOk<FundOrderReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundOrderReadModel[]>(GetFundOrdersQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund order trades.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync()
    {
        try
        {
            FundOrderTradeReadModel[] result = [.. await _dbFactory.FundDb.GetFundOrderTradesAsync()];
            return new ServiceOk<FundOrderTradeReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundOrderTradeReadModel[]>(GetFundOrderTradesQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund transactions.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            FundTransactionReadModel[] result =
                [.. await _dbFactory.FundDb.GetFundTransactionsAsync(fundId, startDate, endDate)];
            return new ServiceOk<FundTransactionReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundTransactionReadModel[]>(GetFundTransactionsQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund balance.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId)
    {
        try
        {
            var result = await _dbFactory.FundDb.GetFundBalanceAsync(fundId);
            return new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(result));
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundBalanceReadModel>(GetFundBalanceQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets opening fund balance.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(
        int fundId,
        DateOnly valueDate)
    {
        try
        {
            var result = await _dbFactory.FundDb.GetOpeningFundBalanceAsync(fundId, valueDate);
            return new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(result));
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundBalanceReadModel>(GetOpeningFundBalanceQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets closing fund balance.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(
        int fundId,
        DateOnly valueDate)
    {
        try
        {
            var result = await _dbFactory.FundDb.GetClosingFundBalanceAsync(fundId, valueDate);
            return new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(result));
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundBalanceReadModel>(GetClosingFundBalanceQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund P&L report.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            var result = await FundQueryCalculations.GetPnlReportAsync(
                _dbFactory.FundDb,
                fundId,
                startDate,
                endDate).ConfigureAwait(false);
            return new ServiceOk<FundPnlReportReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundPnlReportReadModel>(GetFundPnlReportQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund ID from order ID.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetFundIdFromOrderIdAsync(int orderId)
    {
        try
        {
            var result = await _dbFactory.FundDb.GetFundIdFromOrderIdAsync(orderId);
            return new ServiceOk<ScalarReadModel<int>>(new ScalarReadModel<int>(result));
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<int>>(GetFundIdFromOrderIdQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund win loss ratio.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundWinLossRatioReadModel>> GetFundWinLossRatioAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            var result = await FundQueryCalculations.GetWinLossRatioAsync(
                _dbFactory.FundDb,
                fundId,
                startDate,
                endDate).ConfigureAwait(false);
            return new ServiceOk<FundWinLossRatioReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundWinLossRatioReadModel>(GetFundWinLossRatioQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund drawdown balances.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundDrawdownBalancesReadModel>> GetFundDrawdownBalancesAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            var result = await FundQueryCalculations.GetDrawdownBalancesAsync(
                _dbFactory.FundDb,
                fundId,
                startDate,
                endDate).ConfigureAwait(false);
            return new ServiceOk<FundDrawdownBalancesReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundDrawdownBalancesReadModel>(
                GetFundDrawdownBalancesQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets fund max profit generated.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="tradeDate">The trade date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundMaxProfitGeneratedReadModel>> GetFundMaxProfitGeneratedAsync(
        int fundId,
        DateOnly tradeDate)
    {
        try
        {
            var result = await FundQueryCalculations.GetMaxProfitGeneratedAsync(
                _dbFactory.FundDb,
                fundId,
                tradeDate).ConfigureAwait(false);
            return new ServiceOk<FundMaxProfitGeneratedReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundMaxProfitGeneratedReadModel>(
                GetFundMaxProfitGeneratedQuery.ErrorId,
                ex.Message);
        }
    }

}
