using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event.Denormalizer
{
    /// <summary>
    /// trade order event denormalizer
    /// </summary>
    public class TradeOrderEventDenormalizer :  BaseEventDenormalizer,
        IAsyncEventHandler<OrderPlacedEvent>,
        IAsyncEventHandler<OrderFilledEvent>,
        IAsyncEventHandler<OrderCancelledEvent>,
        IAsyncEventHandler<OrderUpdatedEvent>,
        IAsyncEventHandler<OrderExecutedEvent>
    {
        private const int Err_TradeOrderEventDenormalizer = 6006;
        private readonly ITradeDbContext _dbTrade;
        private readonly ITradeEventProducer _eventProducer;

        /// <summary>
        /// trade order event denormalizer constructor
        /// </summary>
        /// <param name="dbTrade"></param>
        /// <param name="eventProducer"></param>
        /// <param name="logger"></param>
        public TradeOrderEventDenormalizer(
            ITradeDbContext dbTrade,
            ITradeEventProducer eventProducer,
            ILogger<TradeEventDenormalizer> logger) : base(logger)
        {
            _dbTrade = dbTrade;
            _eventProducer = eventProducer;
            SetEventProducer(e => _eventProducer.PostEventAsync((dynamic)e));
        }

        /// <summary>
        /// insert trade ticket on order placed event
        /// </summary>
        /// <param name="e">order placed event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(OrderPlacedEvent e)
            => await DenormalizeAsync(e, Err_TradeOrderEventDenormalizer, () => _dbTrade.DbWriter.InsertTradeTicketAsync(e.TradeTicket));

        /// <summary>
        /// insert trade fills on order filled event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OrderFilledEvent e)
            => await DenormalizeAsync(e, Err_TradeOrderEventDenormalizer, () => _dbTrade.DbWriter.InsertTradeFillsAsync(e.TradeTicketId, e.TradeState, e.TradeFills));

        /// <summary>
        /// set trade ticket trade state to order cancelled
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OrderCancelledEvent e)
            => await DenormalizeAsync(e, Err_TradeOrderEventDenormalizer, () => _dbTrade.DbWriter.UpdateTradeTicketStateAsync(
                tradeTicketId: e.TradeTicketId,
                tradeState: TradeState.OrderCancelled,
                updatedOn: e.CancelledOn,
                updatedBy: e.CancelledBy ));

        /// <summary>
        /// update order price
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OrderUpdatedEvent e) 
            => await DenormalizeAsync(e, Err_TradeOrderEventDenormalizer, () => _dbTrade.DbWriter.UpdateTradeTicketOrderPriceAsync(
                 tradeTicketId: e.TradeTicketId,
                 orderPrice: e.OrderPrice,
                 updatedOn: e.UpdatedOn,
                 updatedBy: e.UpdatedBy ));

        /// <summary>
        /// set trade ticket trade state to order completed
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OrderExecutedEvent e) 
            => await DenormalizeAsync(e, Err_TradeOrderEventDenormalizer, () => _dbTrade.DbWriter.UpdateTradeTicketStateAsync(
                tradeTicketId: e.TradeTicket.Id,
                tradeState: e.TradeTicket.TradeState,
                updatedOn: e.ExecutedOn,
                updatedBy: e.ExecutedBy ));

    }
}
