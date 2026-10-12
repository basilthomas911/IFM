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

public enum CompositionParameter { LoadingMilliseconds, ExecutionMilliseconds, CandidateLifetimeMilliseconds, MaximumQuoteAgeMilliseconds, MaximumQuoteSkewMilliseconds, MinimumDisplayedSize, ParticipationFraction, MaximumUnderlyingSpreadTicks, MaximumLegSpreadTicks, MaximumComboSpreadTicks, TargetDaysToExpiry, MinimumDaysToExpiry, MaximumDaysToExpiry, TargetLegDelta, LegDeltaTolerance, TargetPutDelta, TargetCallDelta, TargetNetDelta, BalanceTolerance, MinimumCreditToWidth, MaximumDebitToWidth, MinimumCreditTicks, MinimumRewardToRisk, MidpointToNaturalFraction, MaximumAdverseMoveTicks, FeePerContract, SlippageTicksPerLeg, FuturesPlannedDistance, FuturesStressDistance, FuturesRollHours }
