using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.ServiceApi;

public interface IOptionPricerQueryApi
{
    Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync();
    Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry);
    Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(int orderId, int tradeId);
}
