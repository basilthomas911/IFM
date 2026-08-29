using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionV1Tests
{
    [Theory]
    [InlineData(TimeFrameType.Daily, 55, 0.65, 30, 1, 14)]
    [InlineData(TimeFrameType.Weekly, 60, 0.68, 60, 7, 45)]
    [InlineData(TimeFrameType.Monthly, 65, 0.70, 90, 21, 90)]
    public void Defaults_are_valid_and_horizon_specific(TimeFrameType horizon, decimal strength,
        decimal confidence, int lifetime, int minimumDte, int maximumDte)
    {
        var value = Parameters(horizon);
        new MarketConditionParameterSetValidationRules().Execute(value).Should().BeEmpty();
        value.Scoring.MinimumStrength.Should().Be(strength);
        value.Scoring.MinimumConfidence.Should().Be(confidence);
        value.Execution.ResultLifetimeSeconds.Should().Be(lifetime);
        value.OptionLiquidity.MinimumDte.Should().Be(minimumDte);
        value.OptionLiquidity.MaximumDte.Should().Be(maximumDte);
    }

    [Fact]
    public void Canonical_payload_and_messagepack_are_stable()
    {
        var baseline = Parameters(TimeFrameType.Daily);
        var value = baseline with
        {
            FuturesLiquidity = baseline.FuturesLiquidity with
            {
                HealthySpreadTicks = 1.0m,
                MaximumTradeableSpreadTicks = 2.00m,
                MinimumBidSize = 5.000m
            }
        };
        MarketConditionParameterPayload.ComputeSha256(value)
            .Should().Be(MarketConditionParameterPayload.ComputeSha256(baseline),
                "canonical decimal formatting must be independent of decimal scale");
        var roundTripped = MessagePackSerializer.Deserialize<MarketConditionParameterSet>(
            MessagePackSerializer.Serialize(value));
        roundTripped.Should().BeEquivalentTo(value);
        MarketConditionParameterPayload.ComputeSha256(roundTripped)
            .Should().Be(MarketConditionParameterPayload.ComputeSha256(value));
    }

    [Fact]
    public void Required_arrays_are_defensively_copied_and_canonically_ordered()
    {
        var categories = new[] { "RateDecision", "HighImpact" };
        var configuration = new MarketConditionEventRiskConfiguration
            { RequiredEventCategories = categories };

        categories[0] = "ChangedAfterConstruction";
        var returned = configuration.RequiredEventCategories;
        returned[0] = "ChangedThroughGetter";

        configuration.RequiredEventCategories.Should().Equal("HighImpact", "RateDecision");
    }

    [Fact]
    public void Validation_rejects_every_nested_configuration_boundary()
    {
        var baseline = Parameters(TimeFrameType.Daily);
        MarketConditionParameterSet[] invalid =
        [
            baseline with { Snapshot = baseline.Snapshot with { FuturesQuoteMaximumAgeSeconds = 0 } },
            baseline with { Session = baseline.Session with { EntryWindowEnd = baseline.Session.EntryWindowStart } },
            baseline with { Session = baseline.Session with { EligibleWeekdays = [] } },
            baseline with { EventRisk = baseline.EventRisk with { RateDecisionAfterMinutes = 0 } },
            baseline with { EventRisk = baseline.EventRisk with { RequiredEventCategories = ["HighImpact", "HighImpact"] } },
            baseline with { MarketIntegrity = baseline.MarketIntegrity with { MaximumOneMinuteMoveAtr = 0m } },
            baseline with { FuturesLiquidity = baseline.FuturesLiquidity with { TickSize = 0m } },
            baseline with { FuturesLiquidity = baseline.FuturesLiquidity with { HealthySpreadTicks = 3m } },
            baseline with { OptionLiquidity = baseline.OptionLiquidity with { MinimumDte = 15, MaximumDte = 14 } },
            baseline with { OptionLiquidity = baseline.OptionLiquidity with { MinimumValidQuoteCoverage = 1.01m } },
            baseline with { OptionLiquidity = baseline.OptionLiquidity with { MaximumMedianRelativeSpread = 0.5m, MaximumP90RelativeSpread = 0.4m } },
            baseline with { OperationalReadiness = baseline.OperationalReadiness with { RequiredHealthSources = [""] } },
            baseline with { WorkflowEligibility = baseline.WorkflowEligibility with { MaximumTriggerAgeSeconds = 0 } },
            baseline with { WorkflowEligibility = baseline.WorkflowEligibility with { BlockingRegimeRestrictions = [] } },
            baseline with { Classification = baseline.Classification with { WeakeningReversalLevel = 0.8m, ExhaustingReversalLevel = 0.7m } },
            baseline with { Scoring = baseline.Scoring with { RegimeAlignmentWeight = 0.31m } },
            baseline with { Scoring = baseline.Scoring with { MinimumConfidence = 1.01m } },
            baseline with { Scoring = baseline.Scoring with { OptionalMissingPenalty = 0.2m, OptionalMissingMaximumPenalty = 0.15m } },
            baseline with { Execution = baseline.Execution with { MaximumExecutionMilliseconds = 0 } },
            baseline with { Snapshot = null! }
        ];

        var rules = new MarketConditionParameterSetValidationRules();
        invalid.Should().OnlyContain(value => rules.Execute(value).Length > 0);
    }

    [Fact]
    public void Execution_identity_is_stable_and_valid()
    {
        var input = Healthy();
        var id = MarketConditionExecutionEntityId.Create(input.WorkflowView.EntityId, input.WorkflowView.WorkflowId);
        id.Format().Should().Contain(".MarketCondition.");
        new MarketConditionExecutionEntityIdValidationRules().Execute(id).Should().BeEmpty();
    }

    [Fact]
    public void Healthy_aligned_input_is_tradeable_and_deterministic()
    {
        var input = Healthy(); var model = new MarketConditionCalculationModel();
        var first = model.Calculate(input); var second = model.Calculate(input);
        first.Should().BeEquivalentTo(second);
        first.Tradeability.Should().Be(MarketTradeability.Tradeable);
        first.ConditionType.Should().Be(MarketConditionType.Directional);
        first.Direction.Should().Be(MarketConditionDirection.Bullish);
        first.BlockingReasons.Should().BeEmpty();
        first.SummaryText.Should().Contain("Daily ES condition is Tradeable");
    }

    [Theory]
    [InlineData("session", MarketConditionReasonCodes.Session)]
    [InlineData("event", MarketConditionReasonCodes.EventRisk)]
    [InlineData("integrity", MarketConditionReasonCodes.MarketDislocated)]
    [InlineData("futures", MarketConditionReasonCodes.FuturesLiquidity)]
    [InlineData("options", MarketConditionReasonCodes.OptionLiquidity)]
    [InlineData("operations", MarketConditionReasonCodes.Operations)]
    [InlineData("regime", MarketConditionReasonCodes.RegimeNoNewTrade)]
    [InlineData("stale", MarketConditionReasonCodes.DataUnfit)]
    public void Every_hard_gate_completes_as_not_tradeable(string gate, string reason)
    {
        var input = Healthy(); var s = input.Snapshot;
        input = gate switch
        {
            "session" => input with { Snapshot = Seal(s with { SessionState = s.SessionState with { Status = MarketSessionStatus.Closed } }) },
            "event" => input with { Snapshot = Seal(s with { EventRiskState = s.EventRiskState with { Status = MarketEventRiskStatus.Blocked } }) },
            "integrity" => input with { Snapshot = Seal(s with { FuturesQuote = s.FuturesQuote with { OneMinuteMoveAtr = 1.51m } }) },
            "futures" => input with { Snapshot = Seal(s with { FuturesQuote = s.FuturesQuote with { BidSize = 4m } }) },
            "options" => input with { Snapshot = Seal(s with { OptionChainQuality = s.OptionChainQuality with { ValidQuoteCoverage = 0.79m } }) },
            "operations" => input with { Snapshot = Seal(s with { OperationalHealth = s.OperationalHealth.Select((x, i) => i == 0 ? x with { Status = MarketOperationalStatus.Unavailable } : x).ToArray() }) },
            "regime" => input with { RegimeResult = input.RegimeResult with { Fusion = input.RegimeResult.Fusion with { Restrictions = [RegimeRestriction.NoNewTrade] } } },
            "stale" => input with { Snapshot = Seal(s with { FuturesQuote = s.FuturesQuote with { QuoteObservation = s.FuturesQuote.QuoteObservation with { AgeSeconds = 3m } } }) },
            _ => input
        };
        var result = new MarketConditionCalculationModel().Calculate(input);
        result.Tradeability.Should().Be(MarketTradeability.NotTradeable);
        result.BlockingReasons.Should().Contain(x => x.ReasonCode == reason);
    }

    [Fact]
    public void Corrupt_required_metadata_is_failure_not_no_trade()
    {
        var input = Healthy(); var s = input.Snapshot;
        input = input with { Snapshot = Seal(s with { FuturesQuote = s.FuturesQuote with
            { QuoteObservation = s.FuturesQuote.QuoteObservation with { SourceTimestampUtc = default } } }) };
        var action = () => new MarketConditionCalculationModel().Calculate(input);
        action.Should().Throw<MarketConditionCalculationException>()
            .Which.Category.Should().Be(MarketConditionFailureCategory.RequiredInputInvalid);
    }

    [Fact]
    public async Task Parallel_calculations_are_byte_identical()
    {
        var input = Healthy(); var model = new MarketConditionCalculationModel();
        var payloads = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => MessagePackSerializer.Serialize(model.Calculate(input)))));
        payloads.Skip(1).Should().OnlyContain(x => x.SequenceEqual(payloads[0]));
    }

    internal static MarketConditionCalculationInput Healthy(TimeFrameType horizon = TimeFrameType.Daily)
    {
        var now = new DateTime(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc);
        var workflowId = new StrategyWorkflowId(Guid.CreateVersion7(new DateTimeOffset(now)));
        var itiId = FuturesItiSignalEntityId.Create("ESZ6", new DateOnly(2026, 8, 28), horizon);
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(itiId);
        var parameters = Parameters(horizon);
        var observation = (string id, decimal age) => new MarketSourceObservation
        {
            SourceId = id, SourceTimestampUtc = now.AddSeconds(-(double)age), ReceivedAtUtc = now,
            SequenceId = 1, Availability = MarketSourceAvailability.Available,
            Validity = MarketSourceValidity.Valid, AgeSeconds = age
        };
        var snapshot = Seal(new MarketConditionSnapshot
        {
            SnapshotId = Guid.Parse("019917f7-1c00-7000-8000-000000000001"), WorkflowId = workflowId,
            EntityId = entity, FundId = 1, TargetHorizon = horizon, EvaluationTimestampUtc = now,
            MarketDataAsOfUtc = now.AddSeconds(-1), SourceSequenceWatermark = 1,
            FuturesQuote = new() { BidPrice = 6500m, AskPrice = 6500.25m, BidSize = 12m, AskSize = 12m,
                LastPrice = 6500.25m, OneMinuteMoveAtr = 0.1m, QuoteObservation = observation("FuturesQuote", 0.5m),
                TradeObservation = observation("FuturesTrade", 1m) },
            OptionChainQuality = new() { CandidateContractCount = 20, ValidQuoteCount = 19,
                EligibleExpirationCount = 2, HasCalls = true, HasPuts = true, ValidQuoteCoverage = 0.95m,
                MedianRelativeSpread = 0.05m, P90RelativeSpread = 0.10m, MedianBidSize = 3m,
                MedianAskSize = 3m, UnderlyingMismatch = 0.0001m, Observation = observation("OptionChain", 1m) },
            SessionState = new() { Status = MarketSessionStatus.Open, IsEntryWindow = true,
                ExchangeLocalTime = new TimeSpan(11, 0, 0), ExchangeLocalWeekday = DayOfWeek.Friday,
                Observation = observation("Session", 1m) },
            EventRiskState = new() { Status = MarketEventRiskStatus.Clear, Observation = observation("EventRisk", 1m) },
            VolatilityShockState = new() { FiveMinuteRelativeIncrease = 0.01m, Observation = observation("Volatility", 1m) },
            OperationalHealth = parameters.OperationalReadiness.RequiredHealthSources.Select(x =>
                new MarketConditionOperationalHealthItem { SourceId = x, Status = MarketOperationalStatus.Healthy,
                    Observation = observation(x, 1m) }).ToArray(),
            WorkflowEligibility = new() { EntriesEnabled = true, RegimeProducedAtUtc = now.AddSeconds(-2),
                TriggerProducedAtUtc = now.AddSeconds(-1) }
        });
        var trigger = new FuturesItiSignalGeneratedEvent
        {
            Id = Guid.Parse("019917f7-1c00-7000-8000-000000000002"), EntityId = itiId, CreatedOn = now.AddSeconds(-1),
            FuturesItiSignal = new FuturesItiSignalV2ReadModel { ContractId = "ESZ6", ValueDate = itiId.ValueDate,
                TimeFrameStartValueDate = itiId.ValueDate, TimePeriod = horizon, SequenceId = 1, IntrinsicTime = now,
                IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged, BandLevel = 1d, ReversalLevel = 0.1d,
                TradingDays = 1 }
        };
        var regime = new RegimeDiscoveryResult
        {
            ResultId = Guid.Parse("019917f7-1c00-7000-8000-000000000003"), WorkflowId = workflowId,
            EntityId = entity, TriggerEventId = trigger.Id, TargetHorizon = horizon, ProducedAtUtc = now.AddSeconds(-2),
            OverallConfidence = 0.90m, OverallQuality = RegimeOverallQuality.High,
            Trend = new() { IsComplete = true, Direction = RegimeDirection.Up },
            Volatility = new() { IsComplete = true, Change = VolatilityRegimeChange.Stable },
            MarketStructure = new() { IsComplete = true, Classification = MarketStructureClassification.Trending,
                Direction = RegimeDirection.Up },
            Fusion = new() { IsComplete = true, Direction = RegimeDirection.Up, Confidence = 0.90m,
                Quality = RegimeOverallQuality.High }
        };
        return new()
        {
            ResultId = Guid.Parse("019917f7-1c00-7000-8000-000000000004"), InputWorkflowRevision = 2,
            WorkflowView = new() { EntityId = entity, WorkflowId = workflowId, Status = WorkflowStrategyMachineStatus.Started,
                CurrentStage = StrategyWorkflowStage.MarketCondition, WorkflowRevision = 2, FundId = 1,
                MarketConditionParameterSet = parameters,
                MarketConditionParameterPayloadSha256 = MarketConditionParameterPayload.ComputeSha256(parameters) },
            TriggerEvent = trigger, RegimeResult = regime, ParameterSet = parameters, Snapshot = snapshot
        };
    }

    internal static MarketConditionParameterSet Parameters(TimeFrameType horizon) =>
        MarketConditionParameterSet.CreateDefault(Guid.Parse("019917f7-1c00-7000-8000-000000000010"),
            Guid.Parse("019917f7-1c00-7000-8000-000000000011"), 1, horizon);
    static MarketConditionSnapshot Seal(MarketConditionSnapshot snapshot) => MarketConditionSnapshotHash.Seal(snapshot);
}
