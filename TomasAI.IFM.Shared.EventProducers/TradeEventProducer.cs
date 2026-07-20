using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ServiceApi;

namespace TomasAI.IFM.Shared.EventProducers;

/// <summary>
/// Provides functionality to publish option trade and related events to an event broker using Kafka.
/// </summary>
/// <remarks>TradeEventProducer supports a wide range of trade-related event types, including option trade
/// snapshots, order placements, position updates, and plan actions. It is typically used to integrate trading systems
/// with event-driven architectures, enabling downstream consumers to react to trade lifecycle changes in real time.
/// This class is intended for use in production scenarios; a parameterless constructor is available for BDD testing
/// purposes only.</remarks>
public class TradeEventProducer : KafkaEventProducer,  ITradeEventProducer
{
    public TradeEventProducer()
    {
        /// for BDD testing only...
    }

    /// <summary>
    /// option trade event producer
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public TradeEventProducer(IEventProducerOptions options, ILogger<TradeEventProducer> logger):base(options, logger)
    {
    }

    /// <summary>
    /// post option trade event to event broker
    /// </summary>
    /// <param name="event"></param>
    /// <returns></returns>
    public override async Task PostEventAsync(IEvent @event)
    {
        @event.SetEventSource($"{EventTopic.TradeEvents}");
        await (@event switch
        {
            OptionTradeSnapshotEvent e => SendEventAsync(e.OptionTrade.EntityId, e),
            OptionTradeSnapshotCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeSnapshotFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeOrderPlacedEvent e => SendEventAsync(e.OptionTrade.EntityId, e),
            OptionTradeOrderPlacedCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeOrderPlacedFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeToOpenEvent e => SendEventAsync(e.OptionTrade.EntityId, e),
            OptionTradeToOpenCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeToOpenFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeToCloseEvent e => SendEventAsync(e.OptionTrade.EntityId, e),
            OptionTradeToCloseCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeToCloseFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeDailyProfitTargetUpdatedEvent e => SendEventAsync(e.TradeId, e),
            OptionTradeDailyProfitTargetUpdatedCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeDailyProfitTargetUpdatedFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeEndOfDayProcessedEvent e => SendEventAsync(e.EodKey.TradeId, e),
            OptionTradeEndOfDayProcessedCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeEndOfDayProcessedFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradePositionOpenedEvent e => SendEventAsync(e.OptionTradeId, e),
            OptionTradePositionOpenedCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradePositionOpenedFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradePositionClosedEvent e => SendEventAsync(e.OptionTradeId, e),
            OptionTradePositionClosedCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradePositionClosedFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeSpreadDataInsertedEvent e => SendEventAsync(e.OptionTradeSpreadData.Id, e),
            OptionTradeSpreadDataInsertedCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeSpreadDataInsertedFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeSpreadBarDataInsertedEvent e => SendEventAsync(e.OptionTradeSpreadBarData.Id, e),
            OptionTradeSpreadBarDataInsertedCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeSpreadBarDataInsertedFailEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeSpreadBarDataDeletedEvent e => SendEventAsync(new OptionTradeEntityId(e.OrderId, e.TradeId), e),
            OptionTradeSpreadBarDataDeletedCompleteEvent e => SendEventAsync(e.CommandId, e),
            OptionTradeSpreadBarDataDeletedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePositionAddedEvent e => SendEventAsync(e.TradePosition.EntityId, e),
            TradePositionAddedCompleteEvent e => SendEventAsync(e.CommandId, e),
            TradePositionAddedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePositionUpdatedEvent e => StreamEventAsync(e.CommandId, e),
            TradePositionUpdatedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            TradePositionUpdatedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePositionStatusUpdatedEvent e => StreamEventAsync(e.TradeId, e),
            TradePositionStatusUpdatedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            TradePositionStatusUpdatedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePlanUpdatedEvent e => StreamEventAsync(e.TradePlan.TradeId, e),
            TradePlanUpdatedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            TradePlanUpdatedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePlanActionUpdatedEvent e => StreamEventAsync(e.TradePlanAction.Id, e),
            TradePlanActionUpdatedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            TradePlanActionUpdatedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePlanForwardLossLimitClearedEvent e => StreamEventAsync(e.ForwardLossLimitId, e),
            TradePlanForwardLossLimitClearedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            TradePlanForwardLossLimitClearedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePlanForwardLossLimitWarningUpdatedEvent e => StreamEventAsync(e.TradePlanForwardLossLimit.EntityId, e),
            TradePlanForwardLossLimitWarningUpdatedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            TradePlanForwardLossLimitWarningUpdatedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePlanForwardLossLimitReachedUpdatedEvent e => StreamEventAsync(e.TradePlanForwardLossLimit.EntityId, e),
            TradePlanForwardLossLimitReachedUpdatedCompleteEvent e => StreamEventAsync(e.CommandId, e),
            TradePlanForwardLossLimitReachedUpdatedFailEvent e => SendEventAsync(e.CommandId, e),
            TradePositionChangedEvent e => StreamEventAsync(e.CommandId, e),
            OptionTradeLegDataChangedEvent e => StreamEventAsync(e.OptionLegData.OptionLegId, e),
            OptionTradeSpreadDistributionStatisticsChangedEvent e => StreamEventAsync(e.CommandId, e),
            OptionTradeSpreadDistributionStatisticsUpdatedEvent e => StreamEventAsync(e.TradeId, e),
            OptionTradeLegDataUpdatedEvent e => StreamEventAsync(e.OptionLegData.OptionLegId, e),
            OptionTradeDeletedEvent e => SendEventAsync(e.TradeId, e),
            CommandExceptionEvent e => StreamEventAsync(e.CommandId, e),
            _ => Task.CompletedTask
        });
    }
}
