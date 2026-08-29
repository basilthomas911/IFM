using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.BDDTests.Strategy.Workflow.IntrinsicTime;

public sealed class MarketConditionDecisionHintScenarios
{
    [Theory]
    [InlineData(TimeFrameType.Daily, MarketConditionTradeType.Futures)]
    [InlineData(TimeFrameType.Weekly, MarketConditionTradeType.VerticalSpread)]
    public void Given_an_established_directional_decision_when_market_condition_completes_then_the_horizon_hint_is_preferred(
        TimeFrameType horizon, MarketConditionTradeType expected)
    {
        var result = new MarketConditionCalculationModel().Calculate(Scenario(horizon));

        result.Direction.Should().Be(MarketConditionDirection.Bullish);
        result.Phase.Should().Be(MarketConditionPhase.Continuing);
        result.OutputHints.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            TradeType = expected,
            TimeFrame = horizon,
            Suitability = MarketConditionHintSuitability.Preferred,
            IsAdvisory = true
        }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Given_a_monthly_range_contraction_when_market_condition_completes_then_iron_condor_is_preferred()
    {
        var input = Scenario(TimeFrameType.Monthly);
        input = input with { RegimeResult = input.RegimeResult with
        {
            Decision = input.RegimeResult.Decision with
            {
                Direction = RegimeDirection.Neutral,
                TrendPhase = TrendRegimePhase.RangeBound,
                VolatilityChange = VolatilityRegimeChange.Contracting,
                StructureClassification = MarketStructureClassification.Compressing
            }
        }};

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.ConditionType.Should().Be(MarketConditionType.VolatilityContraction);
        result.OutputHints.Single().TradeType.Should().Be(MarketConditionTradeType.IronCondor);
        result.OutputHints.Single().Suitability.Should().Be(MarketConditionHintSuitability.Preferred);
    }

    [Fact]
    public void Given_a_hard_market_blocker_when_hints_are_emitted_then_they_remain_advisory_and_avoid()
    {
        var input = Scenario(TimeFrameType.Daily);
        input = input with { Snapshot = MarketConditionSnapshotHash.Seal(input.Snapshot with
        {
            FuturesQuote = input.Snapshot.FuturesQuote with { BidSize = 0m }
        })};

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Tradeability.Should().Be(MarketTradeability.NotTradeable);
        result.OutputHints.Single().Suitability.Should().Be(MarketConditionHintSuitability.Avoid);
        result.OutputHints.Single().IsAdvisory.Should().BeTrue();
    }

    static MarketConditionCalculationInput Scenario(TimeFrameType horizon)
    {
        var now = new DateTime(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);
        var workflowId = new StrategyWorkflowId(Guid.Parse("01991d1d-f400-7000-8000-000000000001"));
        var signalId = FuturesItiSignalEntityId.Create("ESZ6", new DateOnly(2026, 8, 29), horizon);
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(signalId);
        var parameters = MarketConditionParameterSet.CreateDefault(
            Guid.Parse("01991d1d-f400-7000-8000-000000000002"),
            Guid.Parse("01991d1d-f400-7000-8000-000000000003"), 1, horizon);
        MarketSourceObservation Observation(string id) => new()
        {
            SourceId = id, SourceTimestampUtc = now.AddSeconds(-1), ReceivedAtUtc = now,
            SequenceId = 1, Availability = MarketSourceAvailability.Available,
            Validity = MarketSourceValidity.Valid, AgeSeconds = 1m
        };
        var snapshot = MarketConditionSnapshotHash.Seal(new MarketConditionSnapshot
        {
            SnapshotId = Guid.Parse("01991d1d-f400-7000-8000-000000000004"), WorkflowId = workflowId,
            EntityId = entity, FundId = 1, TargetHorizon = horizon, EvaluationTimestampUtc = now,
            MarketDataAsOfUtc = now.AddSeconds(-1), SourceSequenceWatermark = 1,
            FuturesQuote = new() { BidPrice = 6500m, AskPrice = 6500.25m, BidSize = 12m, AskSize = 12m,
                LastPrice = 6500.25m, OneMinuteMoveAtr = 0.1m,
                QuoteObservation = Observation("FuturesQuote"), TradeObservation = Observation("FuturesTrade") },
            OptionChainQuality = new() { CandidateContractCount = 20, ValidQuoteCount = 19,
                EligibleExpirationCount = 2, HasCalls = true, HasPuts = true, ValidQuoteCoverage = 0.95m,
                MedianRelativeSpread = 0.05m, P90RelativeSpread = 0.10m, MedianBidSize = 3m,
                MedianAskSize = 3m, UnderlyingMismatch = 0.0001m, Observation = Observation("OptionChain") },
            SessionState = new() { Status = MarketSessionStatus.Open, IsEntryWindow = true,
                ExchangeLocalTime = new TimeSpan(11, 0, 0), ExchangeLocalWeekday = DayOfWeek.Friday,
                Observation = Observation("Session") },
            EventRiskState = new() { Status = MarketEventRiskStatus.Clear, Observation = Observation("EventRisk") },
            VolatilityShockState = new() { FiveMinuteRelativeIncrease = 0.01m,
                Observation = Observation("Volatility") },
            OperationalHealth = parameters.OperationalReadiness.RequiredHealthSources.Select(id =>
                new MarketConditionOperationalHealthItem { SourceId = id, Status = MarketOperationalStatus.Healthy,
                    Observation = Observation(id) }).ToArray(),
            WorkflowEligibility = new() { EntriesEnabled = true, RegimeProducedAtUtc = now.AddSeconds(-2),
                TriggerProducedAtUtc = now.AddSeconds(-1) }
        });
        var trigger = new FuturesItiSignalGeneratedEvent
        {
            Id = Guid.Parse("01991d1d-f400-7000-8000-000000000005"), EntityId = signalId,
            CreatedOn = now.AddSeconds(-1), FuturesItiSignal = new FuturesItiSignalV2ReadModel
            {
                ContractId = "ESZ6", ValueDate = signalId.ValueDate, TimeFrameStartValueDate = signalId.ValueDate,
                TimePeriod = horizon, SequenceId = 1, IntrinsicTime = now,
                IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged,
                BandLevel = 1d, ReversalLevel = 0.1d, TradingDays = 1
            }
        };
        var regime = new RegimeDiscoveryResult
        {
            ResultId = Guid.Parse("01991d1d-f400-7000-8000-000000000006"), WorkflowId = workflowId,
            EntityId = entity, TriggerEventId = trigger.Id, TargetHorizon = horizon,
            ProducedAtUtc = now.AddSeconds(-2), OverallConfidence = 0.90m, OverallQuality = RegimeOverallQuality.High,
            Trend = new() { IsComplete = true, Direction = RegimeDirection.Up, Phase = TrendRegimePhase.Established },
            Volatility = new() { IsComplete = true, Change = VolatilityRegimeChange.Stable },
            MarketStructure = new() { IsComplete = true, Direction = RegimeDirection.Up,
                Classification = MarketStructureClassification.Trending },
            Decision = new() { IsComplete = true, Direction = RegimeDirection.Up, Confidence = 0.90m,
                DirectionalScore = 0.90m, RiskAdjustedConviction = 0.90m,
                TrendPhase = TrendRegimePhase.Established, TrendStrength = TrendRegimeStrength.Strong,
                TrendTimeFrameAgreement = 0.90m, VolatilityLevel = VolatilityRegimeLevel.Normal,
                VolatilityChange = VolatilityRegimeChange.Stable, TermStructure = VxTermStructureRegime.Contango,
                StructureClassification = MarketStructureClassification.Trending }
        };
        return new MarketConditionCalculationInput
        {
            ResultId = Guid.Parse("01991d1d-f400-7000-8000-000000000007"), InputWorkflowRevision = 2,
            WorkflowView = new() { EntityId = entity, WorkflowId = workflowId,
                Status = WorkflowStrategyMachineStatus.Started, CurrentStage = StrategyWorkflowStage.MarketCondition,
                WorkflowRevision = 2, FundId = 1, MarketConditionParameterSet = parameters,
                MarketConditionParameterPayloadSha256 = MarketConditionParameterPayload.ComputeSha256(parameters) },
            TriggerEvent = trigger, RegimeResult = regime, ParameterSet = parameters, Snapshot = snapshot
        };
    }
}
