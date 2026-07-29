using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;

public interface IOptionPricerDbReadContext 
{
    Task<ICollection<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync();
    Task<SpreadDistributionReadModel?> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry);
    Task<int> GetSpreadDistributionJobInProgressCountAsync(int orderId, int tradeId);
}
