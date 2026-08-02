using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.Query.Api;

/// <summary>Provides direct, in-process Option Pricer queries without actor messaging.</summary>
public sealed class ActorOptionPricerQueryApi(IDbContextFactory dbFactory) : IActorOptionPricerQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    public Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync()
        => ExecuteAsync(GetOptionPricerDevicesQuery.ErrorId, async () => new OptionPricerDevicesReadModel
        {
            Devices = [.. await _dbFactory.OptionPricerDb.GetOptionPricerDevicesAsync()]
        });

    public Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(
        int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
        => ExecuteAsync(GetSpreadDistributionQuery.ErrorId,
            async () => (await _dbFactory.OptionPricerDb.GetSpreadDistributionAsync(
                tradeId, tradeType, tradeStatus, valueDate, daysToExpiry))!);

    public Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(
        int orderId, int tradeId)
        => ExecuteAsync(GetSpreadDistributionJobInProgressQuery.ErrorId,
            async () => new ScalarReadModel<bool>(
                await _dbFactory.OptionPricerDb.GetSpreadDistributionJobInProgressCountAsync(orderId, tradeId) > 0));

    static async Task<ServiceResult<T>> ExecuteAsync<T>(int errorId, Func<Task<T>> query)
    {
        try
        {
            return new ServiceOk<T>(await query());
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}
