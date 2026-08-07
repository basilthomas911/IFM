using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.Query.Api;

/// <summary>
/// Provides direct, in-process Option Pricer queries without actor messaging.
/// </summary>
/// <remarks>
/// Device, distribution, and job-state data is read directly through the Option Pricer storage context.
/// Every operation returns a typed service result using its corresponding query error identifier. The
/// implementation does not capture actor context and may be registered as a singleton.
/// </remarks>
public sealed class ActorOptionPricerQueryApi(IDbContextFactory dbFactory) : IActorOptionPricerQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets option pricer devices.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync()
    {
        try
        {
            var result = new OptionPricerDevicesReadModel
            {
                Devices = [.. await _dbFactory.OptionPricerDb.GetOptionPricerDevicesAsync()]
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

    public Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync(
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetOptionPricerDevicesQuery.ErrorId,
            cancellationToken,
            async () => new OptionPricerDevicesReadModel
            {
                Devices = [.. await _dbFactory.OptionPricerDb
                    .GetOptionPricerDevicesAsync(cancellationToken)]
            });

    /// <summary>
    /// Gets spread distribution.
    /// </summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <param name="tradeStatus">The trade lifecycle status.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="daysToExpiry">The number of days remaining until expiry.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(
        int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
    {
        try
        {
            SpreadDistributionReadModel result =
                (await _dbFactory.OptionPricerDb.GetSpreadDistributionAsync(
                    tradeId,
                    tradeType,
                    tradeStatus,
                    valueDate,
                    daysToExpiry))!;
            return new ServiceOk<SpreadDistributionReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<SpreadDistributionReadModel>(
                GetSpreadDistributionQuery.ErrorId,
                ex.Message);
        }
    }

    public Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetSpreadDistributionQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.OptionPricerDb.GetSpreadDistributionAsync(
                tradeId,
                tradeType,
                tradeStatus,
                valueDate,
                daysToExpiry,
                cancellationToken))!);

    /// <summary>
    /// Determines whether spread distribution job in progress.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(
        int orderId, int tradeId)
    {
        try
        {
            var result = new ScalarReadModel<bool>(
                await _dbFactory.OptionPricerDb.GetSpreadDistributionJobInProgressCountAsync(
                    orderId,
                    tradeId) > 0);
            return new ServiceOk<ScalarReadModel<bool>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<bool>>(
                GetSpreadDistributionJobInProgressQuery.ErrorId,
                ex.Message);
        }
    }

    public Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetSpreadDistributionJobInProgressQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<bool>(
                await _dbFactory.OptionPricerDb
                    .GetSpreadDistributionJobInProgressCountAsync(
                        orderId,
                        tradeId,
                        cancellationToken) > 0));

    static async Task<ServiceResult<T>> ExecuteAsync<T>(
        int errorId,
        CancellationToken cancellationToken,
        Func<Task<T>> operation)
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
