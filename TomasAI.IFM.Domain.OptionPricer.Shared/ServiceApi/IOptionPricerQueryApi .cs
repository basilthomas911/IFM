using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;

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
