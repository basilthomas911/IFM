using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionOptionChainQuality
{
    [Key(0)] public int CandidateContractCount { get; init; }
    [Key(1)] public int ValidQuoteCount { get; init; }
    [Key(2)] public int EligibleExpirationCount { get; init; }
    [Key(3)] public bool HasCalls { get; init; }
    [Key(4)] public bool HasPuts { get; init; }
    [Key(5)] public decimal ValidQuoteCoverage { get; init; }
    [Key(6)] public decimal MedianRelativeSpread { get; init; }
    [Key(7)] public decimal P90RelativeSpread { get; init; }
    [Key(8)] public decimal MedianBidSize { get; init; }
    [Key(9)] public decimal MedianAskSize { get; init; }
    [Key(10)] public decimal UnderlyingMismatch { get; init; }
    [Key(11)] public MarketSourceObservation Observation { get; init; } = new();
}
