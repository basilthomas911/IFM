using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

[MessagePackObject]
public sealed record CompositionParameters
{
    [JsonRequired, Key(0)] public int LoadingMilliseconds { get; init; }
    [JsonRequired, Key(1)] public int ExecutionMilliseconds { get; init; }
    [JsonRequired, Key(2)] public int CandidateLifetimeMilliseconds { get; init; }
    [JsonRequired, Key(3)] public int MaximumQuoteAgeMilliseconds { get; init; }
    [JsonRequired, Key(4)] public int MaximumQuoteSkewMilliseconds { get; init; }
    [JsonRequired, Key(5)] public decimal MinimumDisplayedSize { get; init; }
    [JsonRequired, Key(6)] public decimal ParticipationFraction { get; init; }
    [JsonRequired, Key(7)] public decimal MaximumUnderlyingSpreadTicks { get; init; }
    [JsonRequired, Key(8)] public decimal MaximumLegSpreadTicks { get; init; }
    [JsonRequired, Key(9)] public decimal MaximumComboSpreadTicks { get; init; }
    [JsonRequired, Key(10)] public decimal TargetDaysToExpiry { get; init; }
    [JsonRequired, Key(11)] public decimal MinimumDaysToExpiry { get; init; }
    [JsonRequired, Key(12)] public decimal MaximumDaysToExpiry { get; init; }
    [JsonRequired, Key(13)] public decimal TargetLegDelta { get; init; }
    [JsonRequired, Key(14)] public decimal LegDeltaTolerance { get; init; }
    [JsonRequired, Key(15)] public decimal TargetPutDelta { get; init; }
    [JsonRequired, Key(16)] public decimal TargetCallDelta { get; init; }
    [JsonRequired, Key(17)] public decimal TargetNetDelta { get; init; }
    [JsonRequired, Key(18)] public decimal BalanceTolerance { get; init; }
    [JsonRequired, Key(19)] public decimal MinimumCreditToWidth { get; init; }
    [JsonRequired, Key(20)] public decimal MaximumDebitToWidth { get; init; }
    [JsonRequired, Key(21)] public decimal MinimumCreditTicks { get; init; }
    [JsonRequired, Key(22)] public decimal MinimumRewardToRisk { get; init; }
    [JsonRequired, Key(23)] public decimal MidpointToNaturalFraction { get; init; }
    [JsonRequired, Key(24)] public int MaximumAdverseMoveTicks { get; init; }
    [JsonRequired, Key(25)] public decimal FeePerContract { get; init; }
    [JsonRequired, Key(26)] public decimal SlippageTicksPerLeg { get; init; }
    [JsonRequired, Key(27)] public decimal FuturesPlannedDistance { get; init; }
    [JsonRequired, Key(28)] public decimal FuturesStressDistance { get; init; }
    [JsonRequired, Key(29)] public decimal FuturesRollHours { get; init; }
}
