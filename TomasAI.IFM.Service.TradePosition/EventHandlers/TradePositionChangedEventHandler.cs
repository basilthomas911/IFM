using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Service.TradePosition.EventHandlers
{
    /// <summary>
    /// trade position chnaged event handler
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "Existing event service handler identity is used by service registration.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Event service boundary reports failures through failure events, the status console, and structured exception logs.")]
    public async Task ExecuteAsync(TradePositionChangedEvent e)
        {
            ArgumentNullException.ThrowIfNull(e);
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
                        await (_tradeEventProducer.PostEventAsync(putPositionUpdated)).ConfigureAwait(false);
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
                        await (_tradeEventProducer.PostEventAsync(callPositionUpdated)).ConfigureAwait(false);
                        break;
                    case TradePositionChangeSourceType.SpreadDistributionStatistics:
                        await (_tradeEventProducer.PostEventAsync(new OptionTradeSpreadDistributionStatisticsChangedEvent
                        {
                            CommandId = e.CommandId,
                            OrderId = e.TradePositionId.OrderId,
                            TradeId = e.TradePositionId.TradeId,
                            ForwardLossRatio = e.PutTradePosition.LossProbability,
                            ValueDate = e.TradePositionId.ValueDate
                        })).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"{LogSourceType.TradePosition}: trade position {e.OptionLegId} change failed due to: {ex.GetErrorMessage()}";
                await (WriteConsoleAsync(LogSourceType.TradePosition, 6010, errorMessage)).ConfigureAwait(false);
                TradePositionDiagnosticLog.Failed(_logger, ex, errorMessage);
            }
        }

    }
}
