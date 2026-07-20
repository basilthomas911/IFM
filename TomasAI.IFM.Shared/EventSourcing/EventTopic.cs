using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing
{
    /// <summary>
    /// event topic name
    /// </summary>
    public enum EventTopic
    {
        EmptyEvents,
        TradeEvents,
        TradeOrderEvents,
        ReferenceEvents,
        SystemAdminEvents,
        StatusConsoleEvents,
        OptionPricerEvents,
        MarketDataFeedEvents,
        MarketDataEvents,
        FundEvents,
        ExceptionEvents,
        ErrorConsoleEvents,
        TradeStrategyEvents,
        ApplicationEvents,
        MarketDataApiEvents,
        MarketDataAnalyticEvents,
        TradePlacementEvents,
        TelemetryEvents,
        PredictiveModelEvents,
        TradeAlgorithmEvents,
    }

    public static class EventTopicExtensions
    {
        public static string ToStringFast(this EventTopic value) => value switch
        {
            EventTopic.EmptyEvents => nameof(EventTopic.EmptyEvents),
            EventTopic.TradeEvents => nameof(EventTopic.TradeEvents),
            EventTopic.TradeOrderEvents => nameof(EventTopic.TradeOrderEvents),
            EventTopic.ReferenceEvents => nameof(EventTopic.ReferenceEvents),
            EventTopic.SystemAdminEvents => nameof(EventTopic.SystemAdminEvents),
            EventTopic.StatusConsoleEvents => nameof(EventTopic.StatusConsoleEvents),
            EventTopic.OptionPricerEvents => nameof(EventTopic.OptionPricerEvents),
            EventTopic.MarketDataFeedEvents => nameof(EventTopic.MarketDataFeedEvents),
            EventTopic.MarketDataEvents => nameof(EventTopic.MarketDataEvents),
            EventTopic.FundEvents => nameof(EventTopic.FundEvents),
            EventTopic.ExceptionEvents => nameof(EventTopic.ExceptionEvents),
            EventTopic.ErrorConsoleEvents => nameof(EventTopic.ErrorConsoleEvents),
            EventTopic.TradeStrategyEvents => nameof(EventTopic.TradeStrategyEvents),
            EventTopic.ApplicationEvents => nameof(EventTopic.ApplicationEvents),
            EventTopic.MarketDataApiEvents => nameof(EventTopic.MarketDataApiEvents),
            EventTopic.MarketDataAnalyticEvents => nameof(EventTopic.MarketDataAnalyticEvents),
            EventTopic.TradePlacementEvents => nameof(EventTopic.TradePlacementEvents),
            EventTopic.TelemetryEvents => nameof(EventTopic.TelemetryEvents),
            EventTopic.PredictiveModelEvents => nameof(EventTopic.PredictiveModelEvents),
            EventTopic.TradeAlgorithmEvents => nameof(EventTopic.TradeAlgorithmEvents),
            _ => value.ToString()
        };
    }
}
