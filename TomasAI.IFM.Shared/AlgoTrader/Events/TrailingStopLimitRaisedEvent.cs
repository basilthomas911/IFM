using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Shared.AlgoTrader.Events
{
    public record TrailingStopLimitRaisedEvent : TradePlanUpdatedEvent
    {
    }

    public record TrailingStopLimitRaisedCompleteEvent : TradePlanUpdatedCompleteEvent
    {
    }

    public record TrailingStopLimitRaisedFailEvent : TradePlanUpdatedFailEvent
    {
    }
}
