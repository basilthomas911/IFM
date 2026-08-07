using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;

/// <summary>
/// Defines Option Pricer queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorOptionPricerQueryApi : IOptionPricerQueryApi
{
    Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync(
        CancellationToken cancellationToken);

    Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry,
        CancellationToken cancellationToken);

    Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken);
}
