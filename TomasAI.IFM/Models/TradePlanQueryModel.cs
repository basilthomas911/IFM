using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
namespace TomasAI.IFM.Models
{
    public class TradePlanQueryModel : BaseModel<TradePlanQueryModel>
    {
        readonly ITradePlanQueryApi _queryApi;

        /// <summary>
        /// create strade plan query model
        /// </summary>
        public TradePlanQueryModel(ITradePlanQueryApi queryApi)
        {
            _queryApi = queryApi ?? throw new ArgumentNullException(nameof(queryApi));
        }

        /// <summary>
        /// load iron condor forward delta
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="tradeType"></param>
        /// <param name="riskPositionType"></param>
        /// <param name="onCompleted"></param>
        public async Task GetIronCondorForwardDeltaAsync(DateTime valueDate, TradeType tradeType, RiskPositionType riskPositionType, Action<IronCondorForwardDeltaViewModel> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetIronCondorForwardDeltaAsync(valueDate, tradeType, riskPositionType), onCompleted);

        /// <summary>
        /// load trade plans selected trade
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="valueDate"></param>
        /// <param name="onCompleted"></param>
        public async Task GetTradePlansAsync( int orderId, int tradeId, DateTime valueDate, Action<TradePlanReadModel[]> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetTradePlansAsync(orderId, tradeId, valueDate), onCompleted);
    }
}
