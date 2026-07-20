using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event.Denormalizer
{
    public class TradeEventDenormalizer : BaseEventDenormalizer,
        IAsyncEventHandler<OptionTradeOrderPlacedEvent>,
        IAsyncEventHandler<TradePositionAddedEvent>,
        IAsyncEventHandler<TradePositionUpdatedEvent>,
        IAsyncEventHandler<TradePositionStatusUpdatedEvent>,
        IAsyncEventHandler<OptionTradeDailyProfitTargetUpdatedEvent>,
        IAsyncEventHandler<TradePlanUpdatedEvent>,
        IAsyncEventHandler<TradePlanActionUpdatedEvent>,
        IAsyncEventHandler<TradePositionHeldOverEvent>,
        IAsyncEventHandler<HedgePositionOpenedEvent>,
        IAsyncEventHandler<TradePositionHedgedEvent>,
        IAsyncEventHandler<HedgePositionClosedEvent>
    {
        private const int Err_TradeEventDenormalizer = 5006;
        private readonly ITradeDbContext _dbTrade;
        private readonly ITradeEventProducer _eventProducer;

        public TradeEventDenormalizer(
            ITradeDbContext dbTrade, 
            ITradeEventProducer eventProducer, 
            ILogger<TradeEventDenormalizer> logger):base(logger)
        {
            _dbTrade = dbTrade;
            _eventProducer = eventProducer;
            SetEventProducer(e => _eventProducer.PostEventAsync((dynamic)e));
        }

        /// <summary>
        /// insert option trade into query data
        /// </summary>
        /// <param name="e">OptionTradeOrderPlacedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeOrderPlacedEvent e)
            => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => {
                var optionTrade = new OptionTradeViewModel(
                    orderId: e.OptionTrade.OrderId,
                    tradeId: e.OptionTrade.TradeId,
                    tradeStrategy: e.OptionTrade.TradeStrategy,
                    tradeDate: e.OptionTrade.TradeDate,
                    maturityDate: e.OptionTrade.MaturityDate,
                    tradeType: e.OptionTrade.TradeType,
                    tradeState: e.OptionTrade.TradeState,
                    tradeAction: e.OptionTrade.TradeAction,
                    underlyingContractId: e.OptionTrade.UnderlyingContractId,
                    underlyingAssetType: e.OptionTrade.UnderlyingAssetType,
                    isPrimaryTrade: e.OptionTrade.IsPrimaryTrade,
                    isHedgeTrade: e.OptionTrade.IsHedgeTrade,
                    createdOn: e.CreatedOn,
                    createdBy: e.CreatedBy,
                    updatedOn: e.CreatedOn,
                    updatedBy: e.CreatedBy);

                // add option legs...
                optionTrade.AddOptionLegs(e.OptionTrade.OptionLegs);

                // add spread trade data...
                optionTrade.AddTradePosition(e.OptionTrade.TradePosition);

                // set trade limit...
                optionTrade.SetTradeLimit(e.OptionTrade.TradeLimit);

                // add trade type limits...
                optionTrade.AddTradeTypeLimits(e.OptionTrade.TradeTypeLimits);

                // add trade fills if entered...
                optionTrade.AddTradeFills(e.OptionTrade.TradeFills);

                // save option trade...
                return _dbTrade.DbWriter.InsertOptionTradeAsync(optionTrade);
            });

       
        /// <summary>
        /// update trade position in query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePositionAddedEvent e) => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => _dbTrade.DbWriter.InsertTradePositionAsync(e.TradePosition));

        public async Task ExecuteAsync(TradePositionUpdatedEvent e)
            => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => {
                switch (e.TradePositionChangeSource)
                {
                    case Shared.Trade.TradePositionChangeSourceType.PutCreditSpreadLeg:
                        return _dbTrade.DbWriter.InsertTradePositionAsync(e.PutTradePosition);
                    case Shared.Trade.TradePositionChangeSourceType.CallCreditSpreadLeg:
                        return _dbTrade.DbWriter.InsertTradePositionAsync(e.CallTradePosition);
                }
                return Task.CompletedTask;
            });

        /// <summary>
        /// update trade position status in query store
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePositionStatusUpdatedEvent e)
            => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => _dbTrade.DbWriter.UpdateTradePositionStatusAsync(
                        tradeId: e.TradeId,
                        tradeType: e.TradeType,
                        valueDate: e.ValueDate,
                        daysToExpiry: e.DaysToExpiry,
                        oldTradeStatus: e.OldTradeStatus,
                        newTradeStatus: e.NewTradeStatus,
                        updatedOn: e.UpdatedOn,
                        updatedBy: e.UpdatedBy));

        /// <summary>
        /// update daily profit target
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeDailyProfitTargetUpdatedEvent e)
            => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => _dbTrade.DbWriter.UpdateTradeLimitDailyProfitTarget(
                    tradeId: e.TradeId,
                    tradeType: e.TradeType,
                    dailyProfitTarget: e.DailyProfitTarget,
                    updatedOn: e.UpdatedOn,
                    updatedBy: e.UpdatedBy));

        /// <summary>
        /// insert trade plan into query data
        /// </summary>
        /// <param name="e">TradePlanUpdatedEvent</param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePlanUpdatedEvent e) => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => _dbTrade.DbWriter.InsertTradePlanAsync(e.TradePlan));

        /// <summary>
        /// insert trade plan summary
        /// </summary>
        /// <param name="e"></param>
        /// <returns>TradePlanSummaryAddedEvent</returns>
        public async Task ExecuteAsync(TradePlanActionUpdatedEvent e) => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => _dbTrade.DbWriter.InsertTradePlanActionAsync(e.TradePlanAction));

        /// <summary>
        /// insert hold position trade diary entry
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePositionHeldOverEvent e) => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => {
            var tradeDiary = new TradeDiaryEntryReadModel (e.TradePositionId, e.TradePositionAction);
            return _dbTrade.DbWriter.InsertTradeDiaryAsync(tradeDiary);
         });

        /// <summary>
        /// insert add hedge position trade diary entry
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(HedgePositionOpenedEvent e) => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => {
            var tradeDiary = new TradeDiaryEntryReadModel (e.TradePositionId, e.TradePositionAction);
            return _dbTrade.DbWriter.InsertTradeDiaryAsync(tradeDiary);
        });

        /// <summary>
        /// insert hedge trade position trade diary event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePositionHedgedEvent e) => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => {
            var tradeDiary = new TradeDiaryEntryReadModel(e.TradePositionId, e.TradePositionAction);
            return _dbTrade.DbWriter.InsertTradeDiaryAsync(tradeDiary);
        });

        /// <summary>
        /// insert remove hedge position trade diary event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(HedgePositionClosedEvent e) => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () =>{
            var tradeDiary = new TradeDiaryEntryReadModel(e.TradePositionId, e.TradePositionAction);
            return _dbTrade.DbWriter.InsertTradeDiaryAsync(tradeDiary);
        });

        /// <summary>
        /// insert exit trade position trade diary event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePositionOpenedEvent e) => await DenormalizeAsync(e, Err_TradeEventDenormalizer, () => {
            var tradeDiary = new TradeDiaryEntryReadModel(e.TradePositionId, e.TradePositionAction);
            return _dbTrade.DbWriter.InsertTradeDiaryAsync(tradeDiary);
        });
        
    }
}
