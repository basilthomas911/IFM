using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Query.Client;

public class OptionPricerQueryApi(IQueryService querySvc) : IOptionPricerQueryApi
{
    readonly IQueryService _querySvc = IsArgumentNull.Set(querySvc);
    readonly string _controller = "OptionPricer";

    public async Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync()
        => await _querySvc.ExecuteApiQueryAsync(new GetOptionPricerDevicesQuery(), _controller);

    public async Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
        => await _querySvc.ExecuteApiQueryAsync(new GetSpreadDistributionQuery (tradeId, tradeType, tradeStatus, valueDate, daysToExpiry), _controller);

    public async Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(int orderId, int tradeId)
        => await _querySvc.ExecuteApiQueryAsync(new GetSpreadDistributionJobInProgressQuery(orderId, tradeId), _controller);

}
