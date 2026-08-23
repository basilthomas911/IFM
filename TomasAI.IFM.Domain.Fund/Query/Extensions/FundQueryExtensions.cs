using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Query.Extensions;

/// <summary>
/// Provides Fund-specific members and direct query operations for Fund query contexts.
/// </summary>
public static class FundQueryExtensions
{
    extension(IQueryActorContext<FundQueryActor> context)
    {
        /// <summary>
        /// Gets the database-context factory exposed by the underlying <see cref="IFundQueryContext"/>.
        /// </summary>
        public IDbContextFactory DbFactory
            => IsArgumentNull.Set((context as IFundQueryContext)?.DbFactory, nameof(context))!;

        /// <summary>
        /// Gets the logger exposed by the underlying <see cref="IFundQueryContext"/>.
        /// </summary>
        public ILogger<FundQueryActor> Logger
            => IsArgumentNull.Set((context as IFundQueryContext)?.Logger, nameof(context))!;
    }

    extension(IFundQueryContext context)
    {
        /// <inheritdoc cref="IFundQueryApi.GetFundsAsync"/>
        public async Task<ServiceResult<FundReadModel[]>> GetFundsAsync()
        {
            try
            {
                FundReadModel[] result = [.. await context.DbFactory.FundDb.GetFundsAsync()];
                return new ServiceOk<FundReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FundReadModel[]>(GetFundsQuery.ErrorId, ex.Message);
            }
        }

        /// <inheritdoc cref="IFundQueryApi.GetFundOrdersAsync"/>
        public async Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync()
        {
            try
            {
                FundOrderReadModel[] result = [.. await context.DbFactory.FundDb.GetFundOrdersAsync()];
                return new ServiceOk<FundOrderReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FundOrderReadModel[]>(GetFundOrdersQuery.ErrorId, ex.Message);
            }
        }

        /// <inheritdoc cref="IFundQueryApi.GetFundOrderTradesAsync"/>
        public async Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync()
        {
            try
            {
                FundOrderTradeReadModel[] result = [.. await context.DbFactory.FundDb.GetFundOrderTradesAsync()];
                return new ServiceOk<FundOrderTradeReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FundOrderTradeReadModel[]>(GetFundOrderTradesQuery.ErrorId, ex.Message);
            }
        }

        /// <inheritdoc cref="IFundQueryApi.GetFundTransactionsAsync"/>
        public async Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(
            int fundId,
            DateOnly startDate,
            DateOnly endDate)
        {
            try
            {
                FundTransactionReadModel[] result =
                    [.. await context.DbFactory.FundDb.GetFundTransactionsAsync(fundId, startDate, endDate)];
                return new ServiceOk<FundTransactionReadModel[]>(result);
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FundTransactionReadModel[]>(GetFundTransactionsQuery.ErrorId, ex.Message);
            }
        }

        /// <inheritdoc cref="IFundQueryApi.GetFundBalanceAsync"/>
        public async Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId)
        {
            try
            {
                var result = await context.DbFactory.FundDb.GetFundBalanceAsync(fundId);
                return new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(result));
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FundBalanceReadModel>(GetFundBalanceQuery.ErrorId, ex.Message);
            }
        }

        /// <inheritdoc cref="IFundQueryApi.GetOpeningFundBalanceAsync"/>
        public async Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(
            int fundId,
            DateOnly valueDate)
        {
            try
            {
                var result = await context.DbFactory.FundDb.GetOpeningFundBalanceAsync(fundId, valueDate);
                return new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(result));
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FundBalanceReadModel>(GetOpeningFundBalanceQuery.ErrorId, ex.Message);
            }
        }

        /// <inheritdoc cref="IFundQueryApi.GetClosingFundBalanceAsync"/>
        public async Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(
            int fundId,
            DateOnly valueDate)
        {
            try
            {
                var result = await context.DbFactory.FundDb.GetClosingFundBalanceAsync(fundId, valueDate);
                return new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(result));
            }
            catch (Exception ex)
            {
                return new ServiceFailed<FundBalanceReadModel>(GetClosingFundBalanceQuery.ErrorId, ex.Message);
            }
        }

        /// <inheritdoc cref="IFundQueryApi.GetFundPnlReportAsync"/>
        public async Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(
            int fundId,
            DateOnly startDate,
            DateOnly endDate)
        {
            try
            {
                var result = await FundQueryCalculations.GetPnlReportAsync(
                    context.DbFactory.FundDb,
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

        /// <inheritdoc cref="IFundQueryApi.GetFundIdFromOrderIdAsync"/>
        public async Task<ServiceResult<ScalarReadModel<int>>> GetFundIdFromOrderIdAsync(int orderId)
        {
            try
            {
                var result = await context.DbFactory.FundDb.GetFundIdFromOrderIdAsync(orderId);
                return new ServiceOk<ScalarReadModel<int>>(new ScalarReadModel<int>(result));
            }
            catch (Exception ex)
            {
                return new ServiceFailed<ScalarReadModel<int>>(GetFundIdFromOrderIdQuery.ErrorId, ex.Message);
            }
        }

        /// <inheritdoc cref="IFundQueryApi.GetFundWinLossRatioAsync"/>
        public async Task<ServiceResult<FundWinLossRatioReadModel>> GetFundWinLossRatioAsync(
            int fundId,
            DateOnly startDate,
            DateOnly endDate)
        {
            try
            {
                var result = await FundQueryCalculations.GetWinLossRatioAsync(
                    context.DbFactory.FundDb,
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

        /// <inheritdoc cref="IFundQueryApi.GetFundDrawdownBalancesAsync"/>
        public async Task<ServiceResult<FundDrawdownBalancesReadModel>> GetFundDrawdownBalancesAsync(
            int fundId,
            DateOnly startDate,
            DateOnly endDate)
        {
            try
            {
                var result = await FundQueryCalculations.GetDrawdownBalancesAsync(
                    context.DbFactory.FundDb,
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
        /// Gets the maximum-profit generation data for a Fund and trade date.
        /// </summary>
        public async Task<ServiceResult<FundMaxProfitGeneratedReadModel>> GetFundMaxProfitGeneratedAsync(
            int fundId,
            DateOnly tradeDate)
        {
            try
            {
                var result = await FundQueryCalculations.GetMaxProfitGeneratedAsync(
                    context.DbFactory.FundDb,
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
}
