using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.Domain.Trade.Shared.AlgoTrader.Events
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
