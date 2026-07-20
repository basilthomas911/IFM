using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.AlgoTrader.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;

namespace TomasAI.IFM.Application.Event
{
    public class TradeEvents : BaseEvents,
        IAsyncEventHandler<OptionTradeOrderPlacedEvent>,
        IAsyncEventHandler<OptionTradeOrderFilledEvent>,
        IAsyncEventHandler<TradePositionAddedEvent>,
        IAsyncEventHandler<TradePositionChangedEvent>,
        IAsyncEventHandler<OptionTradeEndOfDayProcessedEvent>,
        IAsyncEventHandler<OptionTradeDeletedEvent>,
        IAsyncEventHandler<OptionTradeDistributionStatisticsChangedEvent>,
        IAsyncEventHandler<OptionTradeDailyProfitTargetUpdatedEvent>,
        IAsyncEventHandler<TradePlanUpdatedEvent>
    {
        private readonly ITradePositionFeedServiceApi _tradePositionFeedServiceApi;
        private readonly ITradeEventDenormalizerApi _tradeDenormalizer;
        private readonly IAlgoTraderServiceApi _algoTraderServiceApi;
        private readonly IOptionPricerEventDenormalizerApi _optionPricerDenormalizer;

        /// <summary>
        /// created event handlers for option trade generated events
        /// </summary>
        /// <param name="dbTrade"></param>
        /// <param name="logger"></param>
        public TradeEvents(
            ITradePositionFeedServiceApi tradePositionFeedServiceApi, 
            ITradeEventDenormalizerApi tradeDenormalizer,
            IAlgoTraderServiceApi algoTraderServiceApi,
            IOptionPricerEventDenormalizerApi optionPricerDenormalizer,
            IStatusConsoleServiceApi statusConsoleLog) : base(statusConsoleLog)
        {
            _tradePositionFeedServiceApi = tradePositionFeedServiceApi;
            _tradeDenormalizer = tradeDenormalizer;
            _algoTraderServiceApi = algoTraderServiceApi;
            _optionPricerDenormalizer = optionPricerDenormalizer;
        }

        /// <summary>
        /// execute handler when option trade placed event is generated
        /// </summary>
        /// <param name="e">option trade placed event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeOrderPlacedEvent e) => await _tradeDenormalizer.InsertOptionTradeAsync(e);

        /// <summary>
        /// execute option trade filled event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeOrderFilledEvent e) => await _tradeDenormalizer.InsertTradeFillsAsync(e);

        /// <summary>
        /// execute trade position changed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePositionChangedEvent e)
        {
            switch(e.TradePositionChangeSource)
            {
                case TradePositionChangeSourceType.PutCreditSpreadLeg:
                    var putPositionUpdated = new TradePositionUpdatedEvent {
                        CommandId = e.CommandId,
                        TradePositionChangeSource = TradePositionChangeSourceType.PutCreditSpreadLeg,
                        PutTradePosition = e.PutTradePosition,
                        CallTradePosition = e.CallTradePosition,
                        OptionLegId = e.OptionLegId,
                        UpdatedOn = e.UpdatedOn,
                        UpdatedBy = e.UpdatedBy
                    };
                    await _tradeDenormalizer.UpdateTradePositionAsync(putPositionUpdated);
                    break;
                case TradePositionChangeSourceType.CallCreditSpreadLeg:
                    var callPositionUpdated = new TradePositionUpdatedEvent {
                        CommandId = e.CommandId,
                        TradePositionChangeSource = TradePositionChangeSourceType.CallCreditSpreadLeg,
                        PutTradePosition = e.PutTradePosition,
                        CallTradePosition = e.CallTradePosition,
                        OptionLegId = e.OptionLegId,
                        UpdatedOn = e.UpdatedOn,
                        UpdatedBy = e.UpdatedBy
                    };
                    await _tradeDenormalizer.UpdateTradePositionAsync(callPositionUpdated);
                    break;
                case TradePositionChangeSourceType.SpreadDistributionStatistics:
                    await _algoTraderServiceApi.UpdateTradePlanAsync(new TradeDistributionStatisticsUpdatedEvent {
                        CommandId = e.CommandId,
                        OrderId = e.OrderId,
                        TradeId = e.PutTradePosition.Key.TradeId,
                        ForwardLossRatio = e.PutTradePosition.LossProbability,
                        ValueDate = e.PutTradePosition.Key.ValueDate
                    });
                    break;
            }
        }

        public async Task ExecuteAsync(TradePositionAddedEvent e)
        {
            await _tradeDenormalizer.InsertTradePositionAsync(e);
        }

        public async Task ExecuteAsync(OptionTradeLegDataChangedEvent e)
        {
            try
            {
                await _tradeDenormalizer.UpdateOptionTradeLegDataAsync(new OptionTradeLegDataUpdatedEvent {
                    CommandId = e.CommandId,
                    Key = e.Key,
                    OptionLegData = e.OptionLegData,
                    OrderId = e.OrderId,
                    AssetPrice = e.AssetPrice,
                    CreatedOn = e.CreatedOn,
                    CreatedBy = e.CreatedBy,
                    UpdatedOn = e.UpdatedOn,
                    UpdatedBy = e.UpdatedBy
                });
            }
            catch(Exception ex)
            {
                Console.WriteLine("");
            }
        }

            /// <summary>
            /// execute end of day processed event handler
            /// </summary>
            /// <param name="e"></param>
            /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeEndOfDayProcessedEvent e)
        {
            await _tradeDenormalizer.UpdateTradePositionStatusAsync(new TradePositionStatusUpdatedEvent
            {
                    TradeId = e.EodKey.TradeId,
                    TradeType = TradeType.PutCreditSpread,
                    ValueDate = e.EodKey.ValueDate,
                    DaysToExpiry = e.EodKey.DaysToExpiry,
                    OldTradeStatus = TradeStatus.IntraDay,
                    NewTradeStatus = TradeStatus.EndOfDay,
                    UpdatedOn = e.UpdatedOn,
                    UpdatedBy = e.UpdatedBy
            });
            await _tradeDenormalizer.UpdateTradePositionStatusAsync(new TradePositionStatusUpdatedEvent
            {
                TradeId = e.EodKey.TradeId,
                TradeType = TradeType.CallCreditSpread,
                ValueDate = e.EodKey.ValueDate,
                DaysToExpiry = e.EodKey.DaysToExpiry,
                OldTradeStatus = TradeStatus.IntraDay,
                NewTradeStatus = TradeStatus.EndOfDay,
                UpdatedOn = e.UpdatedOn,
                UpdatedBy = e.UpdatedBy
            });
            await WriteConsoleAsync($"Trade: {e.EodKey.TradeId} EndOfDay Processed for: {e.EodKey.ValueDate.ToString("yyyy-MM-dd")}");
        }

        /// <summary>
        /// delete spread trade from query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeDeletedEvent e) => await _tradeDenormalizer.DeleteOptionTradeAsync(e);

        /// <summary>
        /// insert spread distributions into query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeDistributionStatisticsChangedEvent e)
        {
            await _optionPricerDenormalizer.InsertSpreadDistributionAsync(new SpreadDistributionInsertedEvent { SpreadDistribution = e.PutSpreadDistribution });
            await _optionPricerDenormalizer.InsertSpreadDistributionAsync(new SpreadDistributionInsertedEvent { SpreadDistribution = e.CallSpreadDistribution });
        }

        /// <summary>
        /// update trade limit daily profit target
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeDailyProfitTargetUpdatedEvent e) => await _tradeDenormalizer.UpdateDailyProfitTargetAsync(e);


        /// <summary>
        /// update trade plan for selcted trade
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePlanUpdatedEvent e)
        {
            // await _tradeDenormalizer.InsertTradePlanAsync(e);
            await _algoTraderServiceApi.TradePlanUpdatedAsync(e);
        }

        protected override async Task WriteConsoleAsync(string message) => await WriteConsoleAsync(StatusSourceType.Trade, message);

        protected override async Task WriteConsoleAsync(Exception ex, int errorCode) => await WriteConsoleAsync(StatusSourceType.Trade, ex, errorCode);

    }
}
