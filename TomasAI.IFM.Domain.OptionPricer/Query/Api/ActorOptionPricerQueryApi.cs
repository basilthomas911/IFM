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
}
