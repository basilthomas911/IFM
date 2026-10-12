using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

/// <summary>
/// Carries a non-binding, evidence-derived hint for Trade Selection. The primary Market Condition decision remains
/// authoritative market language; downstream policy may accept, reject, rerank, or augment this hint.
/// </summary>
[MessagePackObject]
public sealed record MarketConditionOutputHint
{
    [Key(0)] public MarketConditionTradeType TradeType { get; init; }
    [Key(1)] public TimeFrameType TimeFrame { get; init; }
    [Key(2)] public MarketConditionHintSuitability Suitability { get; init; }
    [Key(3)] public decimal Confidence { get; init; }
    [Key(4)] public string ReasonCode { get; init; } = string.Empty;
    [Key(5)] public bool IsAdvisory { get; init; } = true;
}
