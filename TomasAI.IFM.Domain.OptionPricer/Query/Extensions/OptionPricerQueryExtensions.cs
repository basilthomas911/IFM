using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.Query.Extensions;

/// <summary>
/// Provides direct, in-process Option Pricer queries without actor messaging.
/// </summary>
/// <remarks>
/// Device, distribution, and job-state data is read directly through the Option Pricer storage context.
/// Every operation returns a typed service result using its corresponding query error identifier. The
/// operations use the readonly services exposed by the typed actor context.
/// </remarks>
public static class OptionPricerQueryExtensions
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
    /// <summary>
    /// Gets option pricer devices.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync(this ISpreadDistributionQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var result = new OptionPricerDevicesReadModel
            {
                Devices = [.. await (context.DbFactory.OptionPricerDb.GetOptionPricerDevicesAsync()).ConfigureAwait(false)]
            };
            return new ServiceOk<OptionPricerDevicesReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<OptionPricerDevicesReadModel>(
                GetOptionPricerDevicesQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>Executes the GetOptionPricerDevicesAsync operation.</summary>
    public static Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync(this ISpreadDistributionQueryContext context,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetOptionPricerDevicesQuery.ErrorId, async () => new OptionPricerDevicesReadModel
            {
                Devices = [.. await (context.DbFactory.OptionPricerDb
                    .GetOptionPricerDevicesAsync(cancellationToken)).ConfigureAwait(false)]
            }, cancellationToken);
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
    /// <summary>
    /// Gets spread distribution.
    /// </summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <param name="tradeStatus">The trade lifecycle status.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="daysToExpiry">The number of days remaining until expiry.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(this ISpreadDistributionQueryContext context,
        int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            SpreadDistributionReadModel result =
                (await (context.DbFactory.OptionPricerDb.GetSpreadDistributionAsync(
                    tradeId,
                    tradeType,
                    tradeStatus,
                    valueDate,
                    daysToExpiry)).ConfigureAwait(false))!;
            return new ServiceOk<SpreadDistributionReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<SpreadDistributionReadModel>(
                GetSpreadDistributionQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>Executes the GetSpreadDistributionAsync operation.</summary>
    public static Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(this ISpreadDistributionQueryContext context,
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetSpreadDistributionQuery.ErrorId, async () => (await (context.DbFactory.OptionPricerDb.GetSpreadDistributionAsync(
                tradeId,
                tradeType,
                tradeStatus,
                valueDate,
                daysToExpiry,
                cancellationToken)).ConfigureAwait(false))!, cancellationToken);
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
    /// <summary>
    /// Determines whether spread distribution job in progress.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(this ISpreadDistributionQueryContext context,
        int orderId, int tradeId)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var result = new ScalarReadModel<bool>(
                await (context.DbFactory.OptionPricerDb.GetSpreadDistributionJobInProgressCountAsync(
                    orderId,
                    tradeId)).ConfigureAwait(false) > 0);
            return new ServiceOk<ScalarReadModel<bool>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<bool>>(
                GetSpreadDistributionJobInProgressQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>Executes the IsSpreadDistributionJobInProgressAsync operation.</summary>
    public static Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(this ISpreadDistributionQueryContext context,
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetSpreadDistributionJobInProgressQuery.ErrorId, async () => new ScalarReadModel<bool>(
                await (context.DbFactory.OptionPricerDb
                    .GetSpreadDistributionJobInProgressCountAsync(
                        orderId,
                        tradeId,
                        cancellationToken)).ConfigureAwait(false) > 0), cancellationToken);
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
    static async Task<ServiceResult<T>> ExecuteAsync<T>(
        int errorId,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await operation().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceOk<T>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}
