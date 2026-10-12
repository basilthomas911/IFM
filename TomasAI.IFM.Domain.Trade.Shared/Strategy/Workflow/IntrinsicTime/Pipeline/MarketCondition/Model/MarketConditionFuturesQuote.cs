using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionFuturesQuote
{
    [Key(0)] public decimal BidPrice { get; init; }
    [Key(1)] public decimal AskPrice { get; init; }
    [Key(2)] public decimal BidSize { get; init; }
    [Key(3)] public decimal AskSize { get; init; }
    [Key(4)] public decimal LastPrice { get; init; }
    [Key(5)] public decimal OneMinuteMoveAtr { get; init; }
    [Key(6)] public MarketSourceObservation QuoteObservation { get; init; } = new();
    [Key(7)] public MarketSourceObservation TradeObservation { get; init; } = new();
}
