using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionGateBoundaryTests
{
    public static TheoryData<string, decimal, bool> Thresholds => new()
    {
        { "RegimeAge", 119m, false }, { "RegimeAge", 120m, false }, { "RegimeAge", 121m, true },
        { "TriggerAge", 29m, false }, { "TriggerAge", 30m, false }, { "TriggerAge", 31m, true },
        { "FuturesQuoteAge", 1.9m, false }, { "FuturesQuoteAge", 2m, false }, { "FuturesQuoteAge", 2.1m, true },
        { "FuturesTradeAge", 4.9m, false }, { "FuturesTradeAge", 5m, false }, { "FuturesTradeAge", 5.1m, true },
        { "OptionChainAge", 9.9m, false }, { "OptionChainAge", 10m, false }, { "OptionChainAge", 10.1m, true },
        { "SessionAge", 59m, false }, { "SessionAge", 60m, false }, { "SessionAge", 61m, true },
        { "EventRiskAge", 899m, false }, { "EventRiskAge", 900m, false }, { "EventRiskAge", 901m, true },
        { "VolatilityAge", 14.9m, false }, { "VolatilityAge", 15m, false }, { "VolatilityAge", 15.1m, true },
        { "HealthAge", 14.9m, false }, { "HealthAge", 15m, false }, { "HealthAge", 15.1m, true },
        { "OneMinuteMove", 1.49m, false }, { "OneMinuteMove", 1.50m, false }, { "OneMinuteMove", 1.51m, true },
        { "VolatilityIncrease", 0.149m, false }, { "VolatilityIncrease", 0.15m, false }, { "VolatilityIncrease", 0.151m, true },
        { "FuturesSpread", 1m, false }, { "FuturesSpread", 2m, false }, { "FuturesSpread", 3m, true },
        { "FuturesBidSize", 4.9m, true }, { "FuturesBidSize", 5m, false }, { "FuturesBidSize", 5.1m, false },
        { "FuturesAskSize", 4.9m, true }, { "FuturesAskSize", 5m, false }, { "FuturesAskSize", 5.1m, false },
        { "CandidateContracts", 11m, true }, { "CandidateContracts", 12m, false }, { "CandidateContracts", 13m, false },
        { "EligibleExpirations", 0m, true }, { "EligibleExpirations", 1m, false }, { "EligibleExpirations", 2m, false },
        { "ValidCoverage", 0.79m, true }, { "ValidCoverage", 0.80m, false }, { "ValidCoverage", 0.81m, false },
        { "MedianSpread", 0.19m, false }, { "MedianSpread", 0.20m, false }, { "MedianSpread", 0.21m, true },
        { "P90Spread", 0.34m, false }, { "P90Spread", 0.35m, false }, { "P90Spread", 0.36m, true },
        { "MedianBidSize", 0.9m, true }, { "MedianBidSize", 1m, false }, { "MedianBidSize", 1.1m, false },
        { "MedianAskSize", 0.9m, true }, { "MedianAskSize", 1m, false }, { "MedianAskSize", 1.1m, false },
        { "UnderlyingMismatch", 0.0024m, false }, { "UnderlyingMismatch", 0.0025m, false }, { "UnderlyingMismatch", 0.0026m, true }
    };

    [Theory]
    [MemberData(nameof(Thresholds))]
    public void Every_numeric_hard_gate_has_explicit_below_equal_above_behavior(
        string threshold, decimal value, bool expectedBlocked)
    {
        var input = Set(MarketConditionV1Tests.Healthy(), threshold, value);

        var result = new MarketConditionCalculationModel().Calculate(input);

        (result.Tradeability == MarketTradeability.NotTradeable).Should().Be(expectedBlocked,
            $"{threshold} at {value} has a specified inclusive boundary");
        if (expectedBlocked)
        {
            result.BlockingReasons.Should().NotBeEmpty();
            result.Strength.Should().Be(0m, "opportunity scoring must not run after a hard blocker");
            result.Confidence.Should().Be(0m, "opportunity scoring must not run after a hard blocker");
        }
    }

    [Theory]
    [InlineData("SessionClosed", MarketConditionReasonCodes.Session)]
    [InlineData("OutsideEntryWindow", MarketConditionReasonCodes.Session)]
    [InlineData("IneligibleWeekday", MarketConditionReasonCodes.Session)]
    [InlineData("EventBlocked", MarketConditionReasonCodes.EventRisk)]
    [InlineData("CrossedMarket", MarketConditionReasonCodes.MarketDislocated)]
    [InlineData("NoCalls", MarketConditionReasonCodes.OptionLiquidity)]
    [InlineData("NoPuts", MarketConditionReasonCodes.OptionLiquidity)]
    [InlineData("FeedUnavailable", MarketConditionReasonCodes.DataUnfit)]
    [InlineData("HealthDegraded", MarketConditionReasonCodes.Operations)]
    [InlineData("HealthUnavailable", MarketConditionReasonCodes.DataUnfit)]
    [InlineData("EntriesDisabled", MarketConditionReasonCodes.WorkflowIneligible)]
    public void Every_categorical_hard_gate_returns_a_stable_business_blocker(string gate, string reason)
    {
        var input = SetCategory(MarketConditionV1Tests.Healthy(), gate);

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Tradeability.Should().Be(MarketTradeability.NotTradeable);
        result.BlockingReasons.Should().Contain(x => x.ReasonCode == reason);
        result.PrimaryReasonCode.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("UnknownSession")]
    [InlineData("UnknownEvent")]
    [InlineData("UnknownHealth")]
    [InlineData("DuplicateHealth")]
    [InlineData("InvalidSource")]
    [InlineData("FutureSource")]
    public void Unknown_invalid_or_contradictory_source_state_is_a_typed_failure(string failure)
    {
        var input = SetInvalid(MarketConditionV1Tests.Healthy(), failure);

        var action = () => new MarketConditionCalculationModel().Calculate(input);

        action.Should().Throw<MarketConditionCalculationException>()
            .Which.Category.Should().Be(MarketConditionFailureCategory.RequiredInputInvalid);
    }

    static MarketConditionCalculationInput Set(MarketConditionCalculationInput input, string key, decimal value)
    {
        var s = input.Snapshot;
        s = key switch
        {
            "RegimeAge" => s with { WorkflowEligibility = s.WorkflowEligibility with
                { RegimeProducedAtUtc = s.EvaluationTimestampUtc.AddSeconds(-(double)value) } },
            "TriggerAge" => s with { WorkflowEligibility = s.WorkflowEligibility with
                { TriggerProducedAtUtc = s.EvaluationTimestampUtc.AddSeconds(-(double)value) } },
            "FuturesQuoteAge" => s with { FuturesQuote = s.FuturesQuote with
                { QuoteObservation = Age(s.FuturesQuote.QuoteObservation, s.EvaluationTimestampUtc, value) } },
            "FuturesTradeAge" => s with { FuturesQuote = s.FuturesQuote with
                { TradeObservation = Age(s.FuturesQuote.TradeObservation, s.EvaluationTimestampUtc, value) } },
            "OptionChainAge" => s with { OptionChainQuality = s.OptionChainQuality with
                { Observation = Age(s.OptionChainQuality.Observation, s.EvaluationTimestampUtc, value) } },
            "SessionAge" => s with { SessionState = s.SessionState with
                { Observation = Age(s.SessionState.Observation, s.EvaluationTimestampUtc, value) } },
            "EventRiskAge" => s with { EventRiskState = s.EventRiskState with
                { Observation = Age(s.EventRiskState.Observation, s.EvaluationTimestampUtc, value) } },
            "VolatilityAge" => s with { VolatilityShockState = s.VolatilityShockState with
                { Observation = Age(s.VolatilityShockState.Observation, s.EvaluationTimestampUtc, value) } },
            "HealthAge" => s with { OperationalHealth = s.OperationalHealth.Select((x, i) => i == 0
                ? x with { Observation = Age(x.Observation, s.EvaluationTimestampUtc, value) } : x).ToArray() },
            "OneMinuteMove" => s with { FuturesQuote = s.FuturesQuote with { OneMinuteMoveAtr = value } },
            "VolatilityIncrease" => s with { VolatilityShockState = s.VolatilityShockState with
                { FiveMinuteRelativeIncrease = value } },
            "FuturesSpread" => s with { FuturesQuote = s.FuturesQuote with
                { AskPrice = s.FuturesQuote.BidPrice + value * input.ParameterSet.FuturesLiquidity.TickSize } },
            "FuturesBidSize" => s with { FuturesQuote = s.FuturesQuote with { BidSize = value } },
            "FuturesAskSize" => s with { FuturesQuote = s.FuturesQuote with { AskSize = value } },
            "CandidateContracts" => s with { OptionChainQuality = s.OptionChainQuality with
                { CandidateContractCount = (int)value } },
            "EligibleExpirations" => s with { OptionChainQuality = s.OptionChainQuality with
                { EligibleExpirationCount = (int)value } },
            "ValidCoverage" => s with { OptionChainQuality = s.OptionChainQuality with { ValidQuoteCoverage = value } },
            "MedianSpread" => s with { OptionChainQuality = s.OptionChainQuality with { MedianRelativeSpread = value } },
            "P90Spread" => s with { OptionChainQuality = s.OptionChainQuality with { P90RelativeSpread = value } },
            "MedianBidSize" => s with { OptionChainQuality = s.OptionChainQuality with { MedianBidSize = value } },
            "MedianAskSize" => s with { OptionChainQuality = s.OptionChainQuality with { MedianAskSize = value } },
            "UnderlyingMismatch" => s with { OptionChainQuality = s.OptionChainQuality with { UnderlyingMismatch = value } },
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
        };
        return input with { Snapshot = MarketConditionSnapshotHash.Seal(s) };
    }

    static MarketConditionCalculationInput SetCategory(MarketConditionCalculationInput input, string key)
    {
        var s = input.Snapshot;
        s = key switch
        {
            "SessionClosed" => s with { SessionState = s.SessionState with { Status = MarketSessionStatus.Closed } },
            "OutsideEntryWindow" => s with { SessionState = s.SessionState with { IsEntryWindow = false } },
            "IneligibleWeekday" => s with { SessionState = s.SessionState with { ExchangeLocalWeekday = DayOfWeek.Sunday } },
            "EventBlocked" => s with { EventRiskState = s.EventRiskState with { Status = MarketEventRiskStatus.Blocked } },
            "CrossedMarket" => s with { FuturesQuote = s.FuturesQuote with { BidPrice = 6501m, AskPrice = 6500m } },
            "NoCalls" => s with { OptionChainQuality = s.OptionChainQuality with { HasCalls = false } },
            "NoPuts" => s with { OptionChainQuality = s.OptionChainQuality with { HasPuts = false } },
            "FeedUnavailable" => s with { FuturesQuote = s.FuturesQuote with { QuoteObservation =
                s.FuturesQuote.QuoteObservation with { Availability = MarketSourceAvailability.Unavailable } } },
            "HealthDegraded" => s with { OperationalHealth = s.OperationalHealth.Select((x, i) => i == 0
                ? x with { Status = MarketOperationalStatus.Degraded } : x).ToArray() },
            "HealthUnavailable" => s with { OperationalHealth = s.OperationalHealth.Select((x, i) => i == 0
                ? x with { Status = MarketOperationalStatus.Unavailable,
                    Observation = x.Observation with { Availability = MarketSourceAvailability.Unavailable } } : x).ToArray() },
            "EntriesDisabled" => s with { WorkflowEligibility = s.WorkflowEligibility with { EntriesEnabled = false } },
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
        };
        return input with { Snapshot = MarketConditionSnapshotHash.Seal(s) };
    }

    static MarketConditionCalculationInput SetInvalid(MarketConditionCalculationInput input, string key)
    {
        var s = input.Snapshot;
        s = key switch
        {
            "UnknownSession" => s with { SessionState = s.SessionState with { Status = MarketSessionStatus.Unknown } },
            "UnknownEvent" => s with { EventRiskState = s.EventRiskState with { Status = MarketEventRiskStatus.Unknown } },
            "UnknownHealth" => s with { OperationalHealth = s.OperationalHealth.Select((x, i) => i == 0
                ? x with { Status = MarketOperationalStatus.Unknown } : x).ToArray() },
            "DuplicateHealth" => s with { OperationalHealth = [.. s.OperationalHealth, s.OperationalHealth[0]] },
            "InvalidSource" => s with { FuturesQuote = s.FuturesQuote with { QuoteObservation =
                s.FuturesQuote.QuoteObservation with { Validity = MarketSourceValidity.Invalid } } },
            "FutureSource" => s with { FuturesQuote = s.FuturesQuote with { QuoteObservation =
                s.FuturesQuote.QuoteObservation with { SourceTimestampUtc = s.EvaluationTimestampUtc.AddSeconds(3) } } },
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
        };
        return input with { Snapshot = MarketConditionSnapshotHash.Seal(s) };
    }

    static MarketSourceObservation Age(MarketSourceObservation value, DateTime at, decimal age) => value with
        { AgeSeconds = age, SourceTimestampUtc = at.AddSeconds(-(double)age) };
}
