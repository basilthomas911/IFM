using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

public interface IOptionPricerDbReadContext 
{
    Task<ICollection<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync();
    Task<ICollection<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync(CancellationToken cancellationToken);
    Task<SpreadDistributionReadModel?> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry);
    Task<SpreadDistributionReadModel?> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry,
        CancellationToken cancellationToken);
    Task<int> GetSpreadDistributionJobInProgressCountAsync(int orderId, int tradeId);
    Task<int> GetSpreadDistributionJobInProgressCountAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken);
}
