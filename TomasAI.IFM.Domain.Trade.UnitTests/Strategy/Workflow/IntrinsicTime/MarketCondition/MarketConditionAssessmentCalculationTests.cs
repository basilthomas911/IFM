using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

[Trait("Gate", "MC-R05")]
public sealed class MarketConditionAssessmentCalculationTests
{
    [Fact]
    public void Newer_covering_calendar_success_replaces_old_scope_for_expiry_without_rejuvenating_download_time()
    {
        var c=AssessmentFixture.Command();var s=Snapshot(c);var at=c.RequestedAtUtc;
        var old=CalendarDownloadFixture.Row("ALL",DateOnly.FromDateTime(at),finished:at.AddDays(-2));
        var recent=CalendarDownloadFixture.Row("US",DateOnly.FromDateTime(at),finished:at.AddHours(-1));
        s=s with {CalendarEvidence=new(){CheckedAtUtc=at,CoverageConfirmed=true,ValidUntilUtc=at.AddHours(23),Attempts=[old,recent]}};
        Calculate(c,s).Assessment.Availability.Should().Be(AssessmentAvailability.Available);
        var stale=s with {CalendarEvidence=s.CalendarEvidence with {Attempts=[old]}};
        Calculate(c,stale).Assessment.Availability.Should().Be(AssessmentAvailability.Unavailable);
    }
    [Theory]
    [InlineData(TimeFrameType.Daily)] [InlineData(TimeFrameType.Weekly)] [InlineData(TimeFrameType.Monthly)]
    public void Given_matching_upstream_and_fresh_required_data_when_assessed_then_only_that_horizon_is_available(TimeFrameType horizon)
    {
        var c = AssessmentFixture.Command(horizon);
        var s = Snapshot(c);
        var r = Calculate(c,s);
        r.TargetHorizon.Should().Be(horizon);
        r.Assessment.EvidenceItems.Should().OnlyContain(x => x.Horizon == horizon);
        r.Assessment.Availability.Should().Be(AssessmentAvailability.Available);
        r.Assessment.ConditionType.Should().Be(AssessmentCondition.Directional);
        r.Assessment.AssessmentConfidence.Should().Be(Math.Round(MarketConditionAssessmentContracts.ValidateRequest(c).Decision.Confidence * .5m,6,MidpointRounding.AwayFromZero));
        r.Assessment.ValidUntilUtc.Should().Be(c.RequestedAtUtc.AddSeconds(1));
        MessagePackSerializer.Serialize(Calculate(c,s)).Should().Equal(MessagePackSerializer.Serialize(r));
    }

    [Theory]
    [InlineData("ReferenceQuote")] [InlineData("FeedHealth")] [InlineData("SessionCalendar")] [InlineData("EventRiskCalendar")]
    public void Missing_required_source_is_completed_unavailable_without_current_condition_or_confidence(string source)
    {
        var c = AssessmentFixture.Command();
        var s = Snapshot(c);
        var r = Calculate(c, s with { Observations = s.Observations.Select(x => x.SourceId == source ? x with { Availability = MarketSourceAvailability.Unavailable } : x).ToArray() });
        r.Assessment.Availability.Should().Be(AssessmentAvailability.Unavailable);
        r.Assessment.ConditionType.Should().BeNull(); r.Assessment.AssessmentConfidence.Should().BeNull(); r.Assessment.ValidUntilUtc.Should().BeNull();
        r.Assessment.LimitationReasons.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(1,10,AssessmentLiquidity.Healthy)] [InlineData(2,5,AssessmentLiquidity.Degraded)] [InlineData(3,20,AssessmentLiquidity.Poor)] [InlineData(1,1,AssessmentLiquidity.Poor)]
    public void Quote_liquidity_describes_quality_without_blocking_available_market(int ticks, int size, AssessmentLiquidity expected)
    {
        var c = AssessmentFixture.Command();
        var r = Calculate(c, Snapshot(c) with { Quote = new(5000,5000+ticks*.25m,size,size) });
        r.Assessment.Availability.Should().Be(AssessmentAvailability.Available);
        r.Assessment.LiquidityCondition.Should().Be(expected);
    }

    [Fact]
    public void Crossed_quote_is_observed_dislocation_and_locked_quote_keeps_size_rules()
    {
        var c = AssessmentFixture.Command();
        var crossed = Calculate(c, Snapshot(c) with { Quote = new(5001,5000,10,10) });
        crossed.Assessment.Availability.Should().Be(AssessmentAvailability.Available);
        crossed.Assessment.ConditionType.Should().Be(AssessmentCondition.Dislocated);
        crossed.Assessment.LiquidityCondition.Should().Be(AssessmentLiquidity.Unknown);
        Calculate(c, Snapshot(c) with { Quote = new(5000,5000,1,1) }).Assessment.LiquidityCondition.Should().Be(AssessmentLiquidity.Poor);
    }

    [Fact]
    public void Missing_optional_stress_stays_unknown_and_does_not_reduce_required_source_confidence()
    {
        var c = AssessmentFixture.Command(); var s = Snapshot(c);
        var r = Calculate(c,s with { Observations = s.Observations.Select(x => x.SourceId == "VolatilityChange" ? x with { Availability = MarketSourceAvailability.Unavailable, Value = null } : x).ToArray() });
        r.Assessment.StressState.Should().Be(AssessmentStress.Unknown);
        r.Assessment.AssessmentConfidence.Should().Be(Calculate(c,s).Assessment.AssessmentConfidence);
    }

    [Theory]
    [InlineData(MarketStructureClassification.Transitioning,VolatilityRegimeChange.Expanding,RegimeDirection.Up,AssessmentCondition.Transition)]
    [InlineData(MarketStructureClassification.Trending,VolatilityRegimeChange.Expanding,RegimeDirection.Up,AssessmentCondition.VolatilityExpansion)]
    [InlineData(MarketStructureClassification.Trending,VolatilityRegimeChange.Contracting,RegimeDirection.Up,AssessmentCondition.VolatilityContraction)]
    [InlineData(MarketStructureClassification.Ranging,VolatilityRegimeChange.Stable,RegimeDirection.Neutral,AssessmentCondition.RangeBound)]
    [InlineData(MarketStructureClassification.Unknown,VolatilityRegimeChange.Unknown,RegimeDirection.Neutral,AssessmentCondition.Unclassified)]
    public void Condition_precedence_preserves_accepted_upstream(MarketStructureClassification structure, VolatilityRegimeChange volatility, RegimeDirection direction, AssessmentCondition expected)
    {
        var c = AssessmentFixture.Command();
        c = WithDecision(c, MarketConditionAssessmentContracts.ValidateRequest(c).Decision with { Direction = direction, StructureClassification = structure, VolatilityChange = volatility });
        var r = Calculate(c,Snapshot(c));
        r.Assessment.ConditionType.Should().Be(expected);
        r.Assessment.UpstreamContext!.Direction.Should().Be(direction);
    }

    [Fact]
    public void Session_closed_event_elevated_low_confidence_and_inherited_restrictions_remain_descriptive()
    {
        var c = AssessmentFixture.Command();
        c = WithDecision(c, MarketConditionAssessmentContracts.ValidateRequest(c).Decision with { Confidence = .01m, Restrictions = [RegimeRestriction.NoNewTrade] });
        var r = Calculate(c, Snapshot(c) with { SessionState = MarketSessionStatus.Closed, EventContext = AssessmentEventContext.Elevated });
        r.Assessment.Availability.Should().Be(AssessmentAvailability.Available);
        r.Assessment.StressState.Should().Be(AssessmentStress.Normal);
        r.Assessment.InheritedRestrictions.Should().Contain(RegimeRestriction.NoNewTrade);
    }

    [Fact]
    public void Required_source_at_maximum_age_is_unavailable_and_invalid_metadata_is_failure()
    {
        var c = AssessmentFixture.Command(); var s = Snapshot(c);
        var aged = s with { Observations = s.Observations.Select(x => x.SourceId == "ReferenceQuote" ? x with { ObservedAtUtc = c.RequestedAtUtc.AddSeconds(-2) } : x).ToArray() };
        Calculate(c,aged).Assessment.Availability.Should().Be(AssessmentAvailability.Unavailable);
        var invalid = s with { Observations = s.Observations.Select(x => x.SourceId == "ReferenceQuote" ? x with { Sequence = -1 } : x).ToArray() };
        Action calculate = () => Calculate(c,invalid);
        calculate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tampered_snapshot_seal_is_rejected()
    {
        var c = AssessmentFixture.Command(); var s = Snapshot(c).Seal();
        Action calculate = () => new MarketConditionAssessmentCalculator().Calculate(c,s with { Quote = new(1,2,1,1) },c.CommandId);
        calculate.Should().Throw<ArgumentException>();
    }

    internal static MarketConditionAssessmentResult Calculate(ExecuteMarketConditionAssessmentCommand c, MarketConditionAssessmentSnapshot s)
        => new MarketConditionAssessmentCalculator().Calculate(c,s.Seal(),c.CommandId);
    internal static MarketConditionAssessmentSnapshot Snapshot(ExecuteMarketConditionAssessmentCommand c) => new()
    {
        SnapshotId = Guid.NewGuid(), MarketProfileId = c.MarketProfileId, InstrumentRoot = c.InstrumentRoot, TargetHorizon = c.TargetHorizon,
        ReferenceInstrumentId = "ES.TEST", EvaluatedAtUtc = c.RequestedAtUtc, Quote = new(5000,5000.25m,10,10), SessionState = MarketSessionStatus.Open, EventContext = AssessmentEventContext.Clear,
        Observations = c.ParameterSet.Sources.Select(x => new AssessmentObservation
        {
            SourceId = x.SourceId, ObservedAtUtc = c.RequestedAtUtc.AddSeconds(-1), ReceivedAtUtc = c.RequestedAtUtc, Sequence = 10,
            Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid, Value = 0, Unit = "ratio"
        }).ToArray(),
        CalendarEvidence = new() { CheckedAtUtc = c.RequestedAtUtc, CoverageConfirmed = true, ValidUntilUtc = c.RequestedAtUtc.AddHours(1) }
    };
    internal static ExecuteMarketConditionAssessmentCommand WithDecision(ExecuteMarketConditionAssessmentCommand c, RegimeDiscoveryDecision decision)
    {
        var r = MarketConditionAssessmentContracts.ValidateRequest(c) with { Decision = decision };
        var e = c.RegimeResultEnvelope with { Payload = MessagePackSerializer.Serialize(r) };
        e = e with { PayloadSha256 = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyStageResultEnvelope.ComputePayloadSha256(e.Payload.Span) };
        return c with { RegimeResultEnvelope = e, RegimePayloadSha256 = e.PayloadSha256, WorkflowView = c.WorkflowView with { RegimeDiscovery = c.WorkflowView.RegimeDiscovery with { Result = e } } };
    }
}
