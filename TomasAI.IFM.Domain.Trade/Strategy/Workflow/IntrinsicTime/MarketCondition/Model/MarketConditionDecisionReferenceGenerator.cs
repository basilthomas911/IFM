using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

/// <summary>Generates a bounded Market Condition reference catalog using the production calculation model.</summary>
public sealed class MarketConditionDecisionReferenceGenerator(MarketConditionCalculationModel? calculationModel = null)
{
    readonly MarketConditionCalculationModel _calculationModel = calculationModel ?? new();

    public MarketConditionDecisionReferenceDto[] Generate() => Cases.Select(Generate).ToArray();

    MarketConditionDecisionReferenceDto Generate(ReferenceCase value)
    {
        var input = ReferenceScenario.Create(value.Horizon);
        var decision = input.RegimeResult.Decision with
        {
            Direction = value.Direction,
            DirectionalScore = value.Direction switch
            {
                RegimeDirection.Up => 0.8m,
                RegimeDirection.Down => -0.8m,
                _ => 0m
            },
            RiskAdjustedConviction = value.Direction == RegimeDirection.Neutral ? 0m : 0.75m,
            Confidence = 0.90m,
            TrendPhase = value.Phase,
            TrendStrength = value.Direction == RegimeDirection.Neutral
                ? TrendRegimeStrength.None : TrendRegimeStrength.Strong,
            TrendTimeFrameAgreement = 0.85m,
            VolatilityLevel = value.NoNewTrade ? VolatilityRegimeLevel.Extreme : VolatilityRegimeLevel.Normal,
            VolatilityChange = value.Volatility,
            TermStructure = value.Volatility == VolatilityRegimeChange.Expanding
                ? VxTermStructureRegime.Backwardation : VxTermStructureRegime.Contango,
            StructureClassification = value.Structure,
            Breakout = value.Breakout,
            Restrictions = value.NoNewTrade ? [RegimeRestriction.NoNewTrade] :
                value.Phase == TrendRegimePhase.Reversing ? [RegimeRestriction.Transition] : []
        };
        input = input with
        {
            RegimeResult = input.RegimeResult with
            {
                Decision = decision,
                Trend = input.RegimeResult.Trend with
                    { Direction = value.Direction, Phase = value.Phase },
                Volatility = input.RegimeResult.Volatility with
                    { Level = decision.VolatilityLevel, Change = value.Volatility,
                        TermStructure = decision.TermStructure, NoNewTrade = value.NoNewTrade },
                MarketStructure = input.RegimeResult.MarketStructure with
                    { Classification = value.Structure, Direction = value.Direction, Breakout = value.Breakout }
            }
        };
        if (value.Direction == RegimeDirection.Down && !value.TriggerConflict)
            input = input with { TriggerEvent = input.TriggerEvent with { FuturesItiSignal =
                input.TriggerEvent.FuturesItiSignal! with { IntrinsicTimeTrend = IntrinsicTimeTrendType.DownTrend } } };
        if (value.OptionBlocker)
            input = input with { Snapshot = MarketConditionSnapshotHash.Seal(input.Snapshot with
                { OptionChainQuality = input.Snapshot.OptionChainQuality with { ValidQuoteCoverage = 0.10m } }) };

        var result = _calculationModel.Calculate(input);
        var hint = result.OutputHints.Single();
        return new MarketConditionDecisionReferenceDto
        {
            CaseCode = value.Code,
            Name = value.Name,
            CoverageTags = value.Tags,
            TargetHorizon = value.Horizon,
            RegimeDirection = value.Direction,
            TrendPhase = value.Phase,
            VolatilityLevel = decision.VolatilityLevel,
            VolatilityChange = value.Volatility,
            TermStructure = decision.TermStructure,
            StructureClassification = value.Structure,
            Breakout = value.Breakout,
            TriggerConflict = value.TriggerConflict,
            OptionQualityBlocked = value.OptionBlocker,
            RegimeNoNewTrade = value.NoNewTrade,
            Tradeability = result.Tradeability,
            ConditionType = result.ConditionType,
            Direction = result.Direction,
            Phase = result.Phase,
            Strength = result.Strength,
            Confidence = result.Confidence,
            VolatilityBehavior = result.VolatilityBehavior,
            LiquidityQuality = result.LiquidityQuality,
            DataQuality = result.DataQuality,
            UpstreamAlignment = result.UpstreamAlignment,
            PrimaryReasonCode = result.PrimaryReasonCode,
            Reasons = result.Reasons,
            BlockingReasons = result.BlockingReasons.Select(reason => reason.ReasonCode).ToArray(),
            EvidenceFeatures = result.EvidenceItems.Select(item => item.FeatureCode).ToArray(),
            HintTradeType = hint.TradeType,
            HintTimeFrame = hint.TimeFrame,
            HintSuitability = hint.Suitability,
            HintConfidence = hint.Confidence,
            HintReasonCode = hint.ReasonCode,
            HintIsAdvisory = hint.IsAdvisory
        };
    }

    static readonly ReferenceCase[] Cases =
    [
        Case("MC-REF-001", "Daily established bullish", ["Daily", "Futures", "Directional", "Bullish"],
            TimeFrameType.Daily, RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeChange.Stable, MarketStructureClassification.Trending),
        Case("MC-REF-002", "Daily established bearish", ["Daily", "Futures", "Directional", "Bearish"],
            TimeFrameType.Daily, RegimeDirection.Down, TrendRegimePhase.Established,
            VolatilityRegimeChange.Stable, MarketStructureClassification.Trending),
        Case("MC-REF-003", "Daily bullish breakout", ["Daily", "Futures", "Breakout"],
            TimeFrameType.Daily, RegimeDirection.Up, TrendRegimePhase.Emerging,
            VolatilityRegimeChange.Stable, MarketStructureClassification.BreakingOut,
            MarketBreakoutState.Up),
        Case("MC-REF-004", "Weekly emerging bullish", ["Weekly", "VerticalSpread", "Emerging"],
            TimeFrameType.Weekly, RegimeDirection.Up, TrendRegimePhase.Emerging,
            VolatilityRegimeChange.Stable, MarketStructureClassification.Trending),
        Case("MC-REF-005", "Weekly volatility expansion", ["Weekly", "VerticalSpread", "Expansion"],
            TimeFrameType.Weekly, RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeChange.Expanding, MarketStructureClassification.Expanding),
        Case("MC-REF-006", "Weekly reversal transition", ["Weekly", "VerticalSpread", "Transition"],
            TimeFrameType.Weekly, RegimeDirection.Up, TrendRegimePhase.Reversing,
            VolatilityRegimeChange.Expanding, MarketStructureClassification.Transitioning),
        Case("MC-REF-007", "Monthly stable range", ["Monthly", "IronCondor", "Range"],
            TimeFrameType.Monthly, RegimeDirection.Neutral, TrendRegimePhase.RangeBound,
            VolatilityRegimeChange.Stable, MarketStructureClassification.Ranging),
        Case("MC-REF-008", "Monthly compression", ["Monthly", "IronCondor", "Compression"],
            TimeFrameType.Monthly, RegimeDirection.Neutral, TrendRegimePhase.RangeBound,
            VolatilityRegimeChange.Contracting, MarketStructureClassification.Compressing),
        Case("MC-REF-009", "Monthly directional market", ["Monthly", "IronCondor", "Directional"],
            TimeFrameType.Monthly, RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeChange.Stable, MarketStructureClassification.Trending),
        Case("MC-REF-010", "Daily trigger conflict", ["Daily", "Futures", "TriggerConflict", "Avoid"],
            TimeFrameType.Daily, RegimeDirection.Down, TrendRegimePhase.Established,
            VolatilityRegimeChange.Stable, MarketStructureClassification.Trending,
            triggerConflict: true),
        Case("MC-REF-011", "Weekly no-new-trade", ["Weekly", "VerticalSpread", "NoNewTrade", "Avoid"],
            TimeFrameType.Weekly, RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeChange.Expanding, MarketStructureClassification.Expanding,
            noNewTrade: true),
        Case("MC-REF-012", "Daily option-quality blocker", ["Daily", "Futures", "OptionQuality", "Avoid"],
            TimeFrameType.Daily, RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeChange.Stable, MarketStructureClassification.Trending,
            optionBlocker: true)
    ];

    static ReferenceCase Case(string code, string name, string[] tags, TimeFrameType horizon,
        RegimeDirection direction, TrendRegimePhase phase, VolatilityRegimeChange volatility,
        MarketStructureClassification structure, MarketBreakoutState breakout = MarketBreakoutState.None,
        bool triggerConflict = false, bool noNewTrade = false, bool optionBlocker = false) =>
        new(code, name, tags, horizon, direction, phase, volatility, structure, breakout,
            triggerConflict, noNewTrade, optionBlocker);

    sealed record ReferenceCase(string Code, string Name, string[] Tags, TimeFrameType Horizon,
        RegimeDirection Direction, TrendRegimePhase Phase, VolatilityRegimeChange Volatility,
        MarketStructureClassification Structure, MarketBreakoutState Breakout,
        bool TriggerConflict, bool NoNewTrade, bool OptionBlocker);

    static class ReferenceScenario
    {
        static readonly DateTime Now = new(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc);

        public static MarketConditionCalculationInput Create(TimeFrameType horizon)
        {
            var workflowId = new StrategyWorkflowId(Guid.Parse("019917f7-1c00-7000-8000-000000000120"));
            var signalId = FuturesItiSignalEntityId.Create("ESZ6", new DateOnly(2026, 8, 28), horizon);
            var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(signalId);
            var parameters = MarketConditionParameterSet.CreateDefault(
                Guid.Parse("019917f7-1c00-7000-8000-000000000121"),
                Guid.Parse("019917f7-1c00-7000-8000-000000000122"), 1, horizon);
            MarketSourceObservation Observation(string id, decimal age) => new()
            {
                SourceId = id,
                SourceTimestampUtc = Now.AddSeconds(-(double)age),
                ReceivedAtUtc = Now,
                SequenceId = 1,
                Availability = MarketSourceAvailability.Available,
                Validity = MarketSourceValidity.Valid,
                AgeSeconds = age
            };
            var snapshot = MarketConditionSnapshotHash.Seal(new MarketConditionSnapshot
            {
                SnapshotId = Guid.Parse("019917f7-1c00-7000-8000-000000000123"),
                WorkflowId = workflowId,
                EntityId = entityId,
                FundId = 1,
                TargetHorizon = horizon,
                EvaluationTimestampUtc = Now,
                MarketDataAsOfUtc = Now.AddSeconds(-1),
                SourceSequenceWatermark = 1,
                FuturesQuote = new MarketConditionFuturesQuote
                {
                    BidPrice = 6500m, AskPrice = 6500.25m, BidSize = 12m, AskSize = 12m,
                    LastPrice = 6500.25m, OneMinuteMoveAtr = 0.1m,
                    QuoteObservation = Observation("FuturesQuote", 0.5m),
                    TradeObservation = Observation("FuturesTrade", 1m)
                },
                OptionChainQuality = new MarketConditionOptionChainQuality
                {
                    CandidateContractCount = 20, ValidQuoteCount = 19, EligibleExpirationCount = 2,
                    HasCalls = true, HasPuts = true, ValidQuoteCoverage = 0.95m,
                    MedianRelativeSpread = 0.05m, P90RelativeSpread = 0.10m,
                    MedianBidSize = 3m, MedianAskSize = 3m, UnderlyingMismatch = 0.0001m,
                    Observation = Observation("OptionChain", 1m)
                },
                SessionState = new MarketConditionSessionState
                {
                    Status = MarketSessionStatus.Open, IsEntryWindow = true,
                    ExchangeLocalTime = new TimeSpan(11, 0, 0), ExchangeLocalWeekday = DayOfWeek.Friday,
                    Observation = Observation("Session", 1m)
                },
                EventRiskState = new MarketConditionEventRiskState
                    { Status = MarketEventRiskStatus.Clear, Observation = Observation("EventRisk", 1m) },
                VolatilityShockState = new MarketConditionVolatilityShockState
                    { FiveMinuteRelativeIncrease = 0.01m, Observation = Observation("Volatility", 1m) },
                OperationalHealth = parameters.OperationalReadiness.RequiredHealthSources.Select(id =>
                    new MarketConditionOperationalHealthItem
                    {
                        SourceId = id, Status = MarketOperationalStatus.Healthy,
                        Observation = Observation(id, 1m)
                    }).ToArray(),
                WorkflowEligibility = new MarketConditionWorkflowEligibilityState
                {
                    EntriesEnabled = true,
                    RegimeProducedAtUtc = Now.AddSeconds(-2),
                    TriggerProducedAtUtc = Now.AddSeconds(-1)
                }
            });
            var trigger = new FuturesItiSignalGeneratedEvent
            {
                Id = Guid.Parse("019917f7-1c00-7000-8000-000000000124"),
                EntityId = signalId,
                CreatedOn = Now.AddSeconds(-1),
                FuturesItiSignal = new FuturesItiSignalV2ReadModel
                {
                    ContractId = "ESZ6", ValueDate = signalId.ValueDate,
                    TimeFrameStartValueDate = signalId.ValueDate, TimePeriod = horizon,
                    SequenceId = 1, IntrinsicTime = Now,
                    IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                    IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
                    BandLevel = 1d, ReversalLevel = 0.1d, TradingDays = 1
                }
            };
            var regime = new RegimeDiscoveryResult
            {
                ResultId = Guid.Parse("019917f7-1c00-7000-8000-000000000125"),
                WorkflowId = workflowId, EntityId = entityId, TriggerEventId = trigger.Id,
                TargetHorizon = horizon, ProducedAtUtc = Now.AddSeconds(-2),
                OverallConfidence = 0.90m, OverallQuality = RegimeOverallQuality.High,
                Trend = new TrendRegimeResult { IsComplete = true, Direction = RegimeDirection.Up },
                Volatility = new VolatilityRegimeResult { IsComplete = true, Change = VolatilityRegimeChange.Stable },
                MarketStructure = new MarketStructureRegimeResult
                {
                    IsComplete = true, Classification = MarketStructureClassification.Trending,
                    Direction = RegimeDirection.Up
                },
                Decision = new RegimeDiscoveryDecision
                {
                    IsComplete = true, Direction = RegimeDirection.Up,
                    Confidence = 0.90m, Quality = RegimeOverallQuality.High
                }
            };
            return new MarketConditionCalculationInput
            {
                ResultId = Guid.Parse("019917f7-1c00-7000-8000-000000000126"),
                InputWorkflowRevision = 2,
                WorkflowView = new IntrinsicTimeStrategyWorkflowView
                {
                    EntityId = entityId, WorkflowId = workflowId,
                    Status = WorkflowStrategyMachineStatus.Started,
                    CurrentStage = StrategyWorkflowStage.MarketCondition,
                    WorkflowRevision = 2, FundId = 1,
                    MarketConditionParameterSet = parameters,
                    MarketConditionParameterPayloadSha256 = MarketConditionParameterPayload.ComputeSha256(parameters)
                },
                TriggerEvent = trigger,
                RegimeResult = regime,
                ParameterSet = parameters,
                Snapshot = snapshot
            };
        }
    }
}
