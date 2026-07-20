using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;

namespace TomasAI.IFM.Service.TradePosition.EventHandlers
{
    /// <summary>
    /// trade position chnaged event handler
    /// </summary>
    public class TradePositionChangedEventHandler : BaseEventServiceHandler,
        IAsyncEventHandler<TradePositionChangedEvent>
    {
        readonly ITradeEventProducer _tradeEventProducer;
        readonly ILogger _logger;

        /// <summary>
        /// trade position chnaged event handler constructor
        /// </summary>
        /// <param name="tradeEventventProducer"></param>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="logger"></param>
        public TradePositionChangedEventHandler(ITradeEventProducer tradeEventventProducer, IStatusConsoleWriter statusConsoleWriter, ILogger logger) 
            : base(statusConsoleWriter)
        {
            _tradeEventProducer = IsArgumentNull.Set(tradeEventventProducer);
            _logger = IsArgumentNull.Set(logger);
        }

        /// <summary>
        /// change trade position
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePositionChangedEvent e)
        {
            try
            {
                switch (e.TradePositionChangeSource)
                {
                    case TradePositionChangeSourceType.PutCreditSpreadLeg:
                        var putPositionUpdated = new TradePositionUpdatedEvent
                        {
                            CommandId = e.CommandId,
                            TradePositionChangeSource = TradePositionChangeSourceType.PutCreditSpreadLeg,
                            PutTradePosition = e.PutTradePosition,
                            CallTradePosition = e.CallTradePosition,
                            OptionLegId = e.OptionLegId,
                            UpdatedOn = e.UpdatedOn,
                            UpdatedBy = e.UpdatedBy
                        };
                        await _tradeEventProducer.PostEventAsync(putPositionUpdated);
                        break;
                    case TradePositionChangeSourceType.CallCreditSpreadLeg:
                        var callPositionUpdated = new TradePositionUpdatedEvent
                        {
                            CommandId = e.CommandId,
                            TradePositionChangeSource = TradePositionChangeSourceType.CallCreditSpreadLeg,
                            PutTradePosition = e.PutTradePosition,
                            CallTradePosition = e.CallTradePosition,
                            OptionLegId = e.OptionLegId,
                            UpdatedOn = e.UpdatedOn,
                            UpdatedBy = e.UpdatedBy
                        };
                        await _tradeEventProducer.PostEventAsync(callPositionUpdated);
                        break;
                    case TradePositionChangeSourceType.SpreadDistributionStatistics:
                        await _tradeEventProducer.PostEventAsync(new OptionTradeDistributionStatisticsUpdatedEvent
                        {
                            CommandId = e.CommandId,
                            OrderId = e.TradePositionId.OrderId,
                            TradeId = e.TradePositionId.TradeId,
                            ForwardLossRatio = e.PutTradePosition.LossProbability,
                            ValueDate = e.TradePositionId.ValueDate
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"{LogSourceType.TradePosition}: trade position {e.OptionLegId} change failed due to: {ex.GetErrorMessage()}";
                await WriteConsoleAsync(LogSourceType.TradePosition, 6010, errorMessage);
                _logger.LogError(ex, errorMessage);
            }
        }

    }
}
