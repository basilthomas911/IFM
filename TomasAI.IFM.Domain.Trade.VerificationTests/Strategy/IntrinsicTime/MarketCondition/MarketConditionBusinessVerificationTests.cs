using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.MarketCondition;

[Trait("Category", "Verification")]
public sealed class MarketConditionBusinessVerificationTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Healthy_aligned_horizon_produces_reviewable_tradeable_result(TimeFrameType horizon)
    {
        var input = MarketConditionVerificationScenario.Healthy(horizon);

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Tradeability.Should().Be(MarketTradeability.Tradeable);
        result.ConditionType.Should().Be(MarketConditionType.Directional);
        result.Direction.Should().Be(MarketConditionDirection.Bullish);
        result.Phase.Should().Be(MarketConditionPhase.Initiating);
        result.Strength.Should().BeGreaterThanOrEqualTo(input.ParameterSet.Scoring.MinimumStrength);
        result.Confidence.Should().BeGreaterThanOrEqualTo(input.ParameterSet.Scoring.MinimumConfidence);
        result.TargetHorizon.Should().Be(horizon);
        result.MarketConditionParameterSetId.Should().Be(input.ParameterSet.ParameterSetId);
        result.SnapshotSha256.Should().Be(input.Snapshot.SnapshotSha256);
        result.SummaryText.Should().Contain($"{horizon} ES condition is Tradeable");
    }

    [Fact]
    public void Aligned_bearish_inputs_produce_bearish_directional_result()
    {
        var input = MarketConditionVerificationScenario.Healthy();
        input = input with
        {
            TriggerEvent = input.TriggerEvent with
            {
                FuturesItiSignal = input.TriggerEvent.FuturesItiSignal! with
                    { IntrinsicTimeTrend = IntrinsicTimeTrendType.DownTrend }
            },
            RegimeResult = input.RegimeResult with
            {
                Trend = input.RegimeResult.Trend with { Direction = RegimeDirection.Down },
                MarketStructure = input.RegimeResult.MarketStructure with { Direction = RegimeDirection.Down },
                Fusion = input.RegimeResult.Fusion with { Direction = RegimeDirection.Down }
            }
        };

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Tradeability.Should().Be(MarketTradeability.Tradeable);
        result.Direction.Should().Be(MarketConditionDirection.Bearish);
        result.ConditionType.Should().Be(MarketConditionType.Directional);
        result.UpstreamAlignment.Should().Be(MarketConditionUpstreamAlignment.Aligned);
    }

    [Theory]
    [InlineData(false, MarketConditionType.RangeBound)]
    [InlineData(true, MarketConditionType.Transition)]
    public void Neutral_and_transition_regimes_produce_expected_classification(
        bool transition,
        MarketConditionType expected)
    {
        var input = MarketConditionVerificationScenario.Healthy();
        input = input with
        {
            RegimeResult = input.RegimeResult with
            {
                MarketStructure = input.RegimeResult.MarketStructure with
                {
                    Classification = transition
                        ? MarketStructureClassification.Transitioning
                        : MarketStructureClassification.Ranging,
                    Direction = RegimeDirection.Neutral
                },
                Fusion = input.RegimeResult.Fusion with
                {
                    Direction = RegimeDirection.Neutral,
                    Restrictions = transition ? [RegimeRestriction.Transition] : []
                }
            }
        };

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Tradeability.Should().Be(MarketTradeability.Tradeable);
        result.ConditionType.Should().Be(expected);
    }

    [Theory]
    [InlineData("session", MarketConditionReasonCodes.Session)]
    [InlineData("stale", MarketConditionReasonCodes.DataUnfit)]
    [InlineData("futures", MarketConditionReasonCodes.FuturesLiquidity)]
    [InlineData("options", MarketConditionReasonCodes.OptionLiquidity)]
    [InlineData("event", MarketConditionReasonCodes.EventRisk)]
    [InlineData("operations", MarketConditionReasonCodes.Operations)]
    [InlineData("regime", MarketConditionReasonCodes.RegimeNoNewTrade)]
    [InlineData("strength", MarketConditionReasonCodes.Strength)]
    [InlineData("confidence", MarketConditionReasonCodes.Confidence)]
    public void Business_blockers_complete_as_no_trade_with_stable_reason(string scenario, string reason)
    {
        var input = MarketConditionVerificationScenario.Blocked(scenario);

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Tradeability.Should().Be(MarketTradeability.NotTradeable);
        result.ConditionType.Should().Be(MarketConditionType.NoOpportunity);
        result.PrimaryReasonCode.Should().Be(reason);
        result.BlockingReasons.Should().Contain(x => x.ReasonCode == reason);
    }

    [Fact]
    public void Corrupt_mandatory_metadata_is_a_typed_failure_not_a_business_blocker()
    {
        var input = MarketConditionVerificationScenario.Healthy();
        var snapshot = input.Snapshot with
        {
            FuturesQuote = input.Snapshot.FuturesQuote with
            {
                QuoteObservation = input.Snapshot.FuturesQuote.QuoteObservation with
                    { SourceTimestampUtc = default }
            }
        };
        input = input with { Snapshot = MarketConditionSnapshotHash.Seal(snapshot) };

        var action = () => new MarketConditionCalculationModel().Calculate(input);

        action.Should().Throw<MarketConditionCalculationException>()
            .Which.Category.Should().Be(MarketConditionFailureCategory.RequiredInputInvalid);
    }

    [Fact]
    public void Repeated_production_calculation_is_byte_identical()
    {
        var input = MarketConditionVerificationScenario.Healthy();
        var model = new MarketConditionCalculationModel();

        var first = MessagePackSerializer.Serialize(model.Calculate(input));
        var second = MessagePackSerializer.Serialize(model.Calculate(input));

        second.Should().Equal(first);
    }
}

static class MarketConditionVerificationScenario
{
    static readonly DateTime Now = new(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc);

    public static MarketConditionCalculationInput Healthy(TimeFrameType horizon = TimeFrameType.Daily)
    {
        var workflowId = new StrategyWorkflowId(Guid.Parse("019917f7-1c00-7000-8000-000000000020"));
        var signalId = FuturesItiSignalEntityId.Create("ESZ6", new DateOnly(2026, 8, 28), horizon);
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(signalId);
        var parameters = MarketConditionParameterSet.CreateDefault(
            Guid.Parse("019917f7-1c00-7000-8000-000000000021"),
            Guid.Parse("019917f7-1c00-7000-8000-000000000022"), 1, horizon);
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
            SnapshotId = Guid.Parse("019917f7-1c00-7000-8000-000000000023"),
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
            Id = Guid.Parse("019917f7-1c00-7000-8000-000000000024"),
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
            ResultId = Guid.Parse("019917f7-1c00-7000-8000-000000000025"),
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
            Fusion = new MarketRegimeFusionResult
            {
                IsComplete = true, Direction = RegimeDirection.Up,
                Confidence = 0.90m, Quality = RegimeOverallQuality.High
            }
        };
        return new MarketConditionCalculationInput
        {
            ResultId = Guid.Parse("019917f7-1c00-7000-8000-000000000026"),
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

    public static MarketConditionCalculationInput Blocked(string scenario)
    {
        var input = Healthy();
        var snapshot = input.Snapshot;
        switch (scenario)
        {
            case "session":
                snapshot = snapshot with
                    { SessionState = snapshot.SessionState with { Status = MarketSessionStatus.Closed } };
                break;
            case "stale":
                snapshot = snapshot with
                {
                    FuturesQuote = snapshot.FuturesQuote with
                    {
                        QuoteObservation = snapshot.FuturesQuote.QuoteObservation with { AgeSeconds = 3m }
                    }
                };
                break;
            case "futures":
                snapshot = snapshot with { FuturesQuote = snapshot.FuturesQuote with { BidSize = 0m } };
                break;
            case "options":
                snapshot = snapshot with
                    { OptionChainQuality = snapshot.OptionChainQuality with { ValidQuoteCoverage = 0.10m } };
                break;
            case "event":
                snapshot = snapshot with
                    { EventRiskState = snapshot.EventRiskState with { Status = MarketEventRiskStatus.Blocked } };
                break;
            case "operations":
                snapshot = snapshot with
                {
                    OperationalHealth = snapshot.OperationalHealth.Select((item, index) => index == 0
                        ? item with { Status = MarketOperationalStatus.Unavailable }
                        : item).ToArray()
                };
                break;
            case "regime":
                input = input with
                    { RegimeResult = input.RegimeResult with
                        { Fusion = input.RegimeResult.Fusion with { Restrictions = [RegimeRestriction.NoNewTrade] } } };
                break;
            case "strength":
                input = WithScoring(input, input.ParameterSet.Scoring with { MinimumStrength = 100m });
                break;
            case "confidence":
                input = WithScoring(input, input.ParameterSet.Scoring with { MinimumConfidence = 1m });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        return input with { Snapshot = MarketConditionSnapshotHash.Seal(snapshot) };
    }

    static MarketConditionCalculationInput WithScoring(
        MarketConditionCalculationInput input,
        MarketConditionScoringConfiguration scoring)
    {
        var parameters = input.ParameterSet with { Scoring = scoring };
        return input with
        {
            ParameterSet = parameters,
            WorkflowView = input.WorkflowView with
            {
                MarketConditionParameterSet = parameters,
                MarketConditionParameterPayloadSha256 = MarketConditionParameterPayload.ComputeSha256(parameters)
            }
        };
    }
}
