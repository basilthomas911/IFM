using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Query.Client
{
    public class OptionPricerQueryApi : IOptionPricerQueryApi
    {
        readonly IQueryService _querySvc;
        readonly string _controller;

        public OptionPricerQueryApi(IQueryService querySvc)
        {
            _querySvc = IsArgumentNull.Set(querySvc);
            _controller = "OptionPricer";
        }

        public async Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetOptionPricerDevicesQuery{}, _controller);

        public async Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateTime valueDate, int daysToExpiry)
            => await _querySvc.ExecuteApiQueryAsync(new GetSpreadDistributionQuery {TradeId = tradeId, TradeType = tradeType, TradeStatus = tradeStatus, ValueDate = valueDate, DaysToExpiry = daysToExpiry }, _controller);

        public async Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(int orderId, int tradeId)
            => await _querySvc.ExecuteApiQueryAsync(new GetSpreadDistributionJobInProgressQuery{ OrderId = orderId, TradeId = tradeId }, _controller);

    }
}
