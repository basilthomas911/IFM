using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;


namespace TomasAI.IFM.Application.Event
{
    public class FundEvents : BaseEvents,
        IAsyncEventHandler<FundCreatedEvent>,
        IAsyncEventHandler<OrderAddedToFundEvent>,
        IAsyncEventHandler<OrderRemovedFromFundEvent>,
        IAsyncEventHandler<TradeAddedToFundOrderEvent>,
        IAsyncEventHandler<TradeRemovedFromFundOrderEvent>,
        IAsyncEventHandler<FundOrderTradeStateChangedEvent>,
        IAsyncEventHandler<OpeningTradeFundTransactionCreatedEvent>,
        IAsyncEventHandler<OpeningTradeFundTransactionAdjustmentCreatedEvent>,
        IAsyncEventHandler<TradeCommissionFundTransactionCreatedEvent>,
        IAsyncEventHandler<TradeCommissionFundTransactionAdjustmentCreatedEvent>,
        IAsyncEventHandler<UnrealizedTradePnlFundTransactionCreatedEvent>,
        IAsyncEventHandler<UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent>,
        IAsyncEventHandler<RealizedTradePnlFundTransactionCreatedEvent>,
        IAsyncEventHandler<RealizedTradePnlFundTransactionAdjustmentCreatedEvent>,
        IAsyncEventHandler<FundOrderCompletedEvent>
    {
        private const int ErrorCodeBase = 1000;
        private readonly IFundEventDenormalizerApi _fundEventDenormalizer;

        public FundEvents(IFundEventDenormalizerApi fundEventDenormalizer, IStatusConsoleServiceApi statusConsoleLog)
            :base(statusConsoleLog)
        { 
            _fundEventDenormalizer = fundEventDenormalizer;
        }

        /// <summary>
        /// create fund
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FundCreatedEvent e) => await this.ExecuteAsync(ErrorCodeBase+1, () =>_fundEventDenormalizer.InsertFundAsync(e));
        
        /// <summary>
        /// create fund order
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OrderAddedToFundEvent e) => await this.ExecuteAsync(ErrorCodeBase + 1, () =>_fundEventDenormalizer.InsertFundOrderAsync(e));

        /// <summary>
        /// delete fund order from storage
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OrderRemovedFromFundEvent e) => await this.ExecuteAsync(ErrorCodeBase + 2, () => _fundEventDenormalizer.DeleteFundOrderAsync(e));

        /// <summary>
        /// create fund order trade
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradeAddedToFundOrderEvent e) => await this.ExecuteAsync(ErrorCodeBase + 3, () => _fundEventDenormalizer.InsertFundOrderTradeAsync(e));

        /// <summary>
        /// delete trade from storage
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradeRemovedFromFundOrderEvent e) => await this.ExecuteAsync(ErrorCodeBase + 4, () => _fundEventDenormalizer.DeleteFundOrderTradeAsync(e));

        /// <summary>
        /// update fund order trade state
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(FundOrderTradeStateChangedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 5, () => _fundEventDenormalizer.UpdateFundOrderTradeStateAsync(e));

        public async Task ExecuteAsync(OpeningTradeFundTransactionCreatedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 6, () => _fundEventDenormalizer.InsertFundTransactionAsync(e));

        public async Task ExecuteAsync(TradeCommissionFundTransactionCreatedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 7, () => _fundEventDenormalizer.InsertFundTransactionAsync(e));

        public async Task ExecuteAsync(RealizedTradePnlFundTransactionCreatedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 8, () => _fundEventDenormalizer.InsertFundTransactionAsync(e));

        public async Task ExecuteAsync(UnrealizedTradePnlFundTransactionCreatedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 9, () => _fundEventDenormalizer.InsertFundTransactionAsync(e));

        public async Task ExecuteAsync(OpeningTradeFundTransactionAdjustmentCreatedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 10, () => _fundEventDenormalizer.InsertFundTransactionAsync(e));

        public async Task ExecuteAsync(TradeCommissionFundTransactionAdjustmentCreatedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 11, () => _fundEventDenormalizer.InsertFundTransactionAsync(e));

        public async Task ExecuteAsync(UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 12, () => _fundEventDenormalizer.InsertFundTransactionAsync(e));

        public async Task ExecuteAsync(RealizedTradePnlFundTransactionAdjustmentCreatedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 13, () => _fundEventDenormalizer.InsertFundTransactionAsync(e));

        public async Task ExecuteAsync(FundOrderCompletedEvent e) => await this.ExecuteAsync(ErrorCodeBase + 14, () => _fundEventDenormalizer.UpdateFundOrderStatusAsync(e));

        protected override async Task WriteConsoleAsync(Exception ex, int errorCode) => await WriteConsoleAsync(StatusSourceType.Fund, ex, errorCode);

    }
}
