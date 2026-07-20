using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Events;

namespace TomasAI.IFM.Models
{
    public class TradeCommandModel : BaseModel<TradeCommandModel>
    {
        readonly ITradeCommandApi _commandApi;

        /// <summary>
        /// create trade command model
        /// </summary>
        /// <param name="commandApi"></param>
        public TradeCommandModel(
            ITradeCommandApi commandApi)
        {
            _commandApi = commandApi ?? throw new ArgumentNullException(nameof(commandApi));
        }

        /// <summary>
        /// snapshot trade
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        public async Task SnapshotOptionTradeAsync(int orderId, int tradeId) 
            => await ExecuteCommandAsync(() => _commandApi.SnapshotAsync(orderId, tradeId));

        /// <summary>
        /// delete trade
        /// </summary>
        public async Task<Guid> DeleteTradeAsync(int orderId, int tradeId) 
            => await ExecuteCommandAsync( () => _commandApi.DeleteAsync(orderId, tradeId));

        /// <summary>
        /// delete trades
        /// </summary>
        public async Task<Guid> DeleteTradesAsync(int orderId) 
            => await ExecuteCommandAsync( () => _commandApi.DeleteAsync(orderId)); 

        /// <summary>
        /// insert option trade spread data
        /// </summary>
        /// <param name="optionTradeSpreadData"></param>
        /// <returns></returns>
        public async Task<Guid> InsertOptionTradeSpreadDataAsync(OptionTradeSpreadDataViewModel optionTradeSpreadData)
            => await ExecuteCommandAsync(() => _commandApi.InsertOptionTradeSpreadDataAsync(optionTradeSpreadData));

        /// <summary>
        /// delete option trade spread bar data
        /// </summary>
        /// <param name="optionTradeId"></param>
        /// <param name="tradeType"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<Guid>DeleteOptionTradeSpreadBarDataAsync(OptionTradeId optionTradeId, TradeType tradeType, DateTime valueDate)
            => await ExecuteCommandAsync(() => _commandApi.DeleteOptionTradeSpreadBarDataAsync(optionTradeId, tradeType, valueDate));

        /// <summary>
        /// insert option trade spread bar data
        /// </summary>
        /// <param name="optionTradeSpreadBarData"></param>
        /// <returns></returns>
        public async Task<Guid> InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarDataViewModel optionTradeSpreadBarData)
            => await ExecuteCommandAsync(() => _commandApi.InsertOptionTradeSpreadBarDataAsync(optionTradeSpreadBarData));

        /// <summary>
        /// run end of day process
        /// </summary>
        /// <param name="tradeId"></param>
        public async Task<Guid> ProcessEndOfDayAsync(int fundId, int orderId, int tradeId, TradeType tradeType, DateTime valueDate, TradeStatus tradeStatus,
            double openPrice, double highPrice, double lowPrice, double closePrice, int volume, string reference, Action<Guid> setCommandId)
             => await ExecuteCommandAsync(() => _commandApi.ProcessEndOfDayAsync(fundId, orderId, tradeId, tradeType, valueDate, tradeStatus, openPrice, highPrice, lowPrice, closePrice, volume, reference, setCommandId));

        /// <summary>
        /// submit trade broker order to open or close option trade
        /// </summary>
        /// <param name="tradeOrder"></param>
        /// <param name="optionTrade"></param>
        /// <param name="setCommandId"></param>
        public async Task PlaceOrderAsync( TradeOrderReadModel tradeOrder, OptionTradeViewModel optionTrade, Action<Guid> setCommandId) 
            => await ExecuteCommandAsync( () => _commandApi.PlaceOrderAsync(tradeOrder, optionTrade, setCommandId));
       
        /// <summary>
        /// fill spread trade 
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="tradeFills"></param>
        /// <param name="onCompleted"></param>
        public async Task ChangeOptionLegDataAsync(int orderId, int tradeId, TradePositionId key, decimal assetPrice, double riskFreeRate, OptionLegDataReadModel optionLegData)
            => await ExecuteCommandAsync(() => _commandApi.ChangeOptionLegDataAsync(orderId, tradeId, key.TradeType, key.ValueDate, key.TradeStatus, assetPrice, riskFreeRate, optionLegData));

        /// <summary>
        /// change distribution statistics
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ChangeDistributionStatisticsAsync(SpreadDistributionJobReadModel e)
            => await ExecuteCommandAsync( () => _commandApi.ChangeDistributionStatisticsAsync(e.OrderId, e.TradeId, e.TradeType, e.ValueDate, e.TradeStatus, e.PutSpreadDistribution, e.CallSpreadDistribution));

        /// <summary>
        /// update trade limit daily profit target
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        /// <param name="daysToExpiry"></param>
        /// <param name="onCompleted"></param>
        public async Task UpdateTradeLimitDailyProfitTargetAsync(int orderId, int tradeId, int tradingDays, int maxTradingDays) 
            => await ExecuteCommandAsync( () => _commandApi.UpdateTradeLimitDailyProfitTargetAsync(orderId, tradeId, tradingDays, maxTradingDays));

        
    }
}
