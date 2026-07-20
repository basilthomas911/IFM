using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Queries;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Query.SignalRClient.Queries
{
    public class OptionPricerQueryApi : IOptionPricerQueryApi
    {
        private IQueryService _querySvc;

        public OptionPricerQueryApi(IQueryService querySvc)
        {
            _querySvc = querySvc;
        }

        public async Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync()
            => await _querySvc.ExecuteQueryAsync<OptionPricerDevicesReadModel>(new GetOptionPricerDevicesQuery{});

        public async Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateTime valueDate, int daysToExpiry)
            => await _querySvc.ExecuteQueryAsync<SpreadDistributionReadModel>(new GetSpreadDistributionQuery {TradeId = tradeId, TradeType = tradeType, TradeStatus = tradeStatus, ValueDate = valueDate, DaysToExpiry = daysToExpiry });

        public async Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync()
            => await _querySvc.ExecuteQueryAsync<ScalarReadModel<bool>>(new GetSpreadDistributionJobInProgressQuery{});

    }
}
