using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionGoldenVectorTests
{
    [Theory]
    [InlineData("Bullish", MarketConditionType.Directional, MarketConditionDirection.Bullish, 96, 0.931546)]
    [InlineData("Bearish", MarketConditionType.Directional, MarketConditionDirection.Bearish, 96, 0.931546)]
    [InlineData("Range", MarketConditionType.RangeBound, MarketConditionDirection.Bullish, 85, 0.931546)]
    [InlineData("Transition", MarketConditionType.Transition, MarketConditionDirection.Bullish, 85, 0.831546)]
    [InlineData("Expansion", MarketConditionType.VolatilityExpansion, MarketConditionDirection.Bullish, 96, 0.931546)]
    [InlineData("Contraction", MarketConditionType.VolatilityContraction, MarketConditionDirection.Bullish, 85, 0.931546)]
    public void Exact_classification_strength_and_confidence_vectors_are_frozen(string scenario,
        MarketConditionType condition, MarketConditionDirection direction, int strength, double confidence)
    {
        var result = new MarketConditionCalculationModel().Calculate(Scenario(scenario));

        result.ConditionType.Should().Be(condition);
        result.Direction.Should().Be(direction);
        result.Strength.Should().Be(strength);
        result.Confidence.Should().Be((decimal)confidence);
        result.Tradeability.Should().Be(MarketTradeability.Tradeable);
        result.EvidenceItems.Where(x => x.Area == MarketConditionEvidenceArea.Scoring)
            .Should().HaveCount(6).And.OnlyContain(x => x.WeightedContribution >= 0m);
    }

    [Theory]
    [InlineData(95, false)]
    [InlineData(96, false)]
    [InlineData(97, true)]
    public void Strength_threshold_is_inclusive(decimal minimum, bool blocked)
    {
        var input = WithScoring(MarketConditionV1Tests.Healthy(),
            MarketConditionV1Tests.Healthy().ParameterSet.Scoring with { MinimumStrength = minimum });

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Strength.Should().Be(96m);
        result.BlockingReasons.Any(x => x.ReasonCode == MarketConditionReasonCodes.Strength).Should().Be(blocked);
    }

    [Theory]
    [InlineData(0.931545, false)]
    [InlineData(0.931546, false)]
    [InlineData(0.931547, true)]
    public void Confidence_threshold_is_inclusive(double minimum, bool blocked)
    {
        var input = MarketConditionV1Tests.Healthy();
        input = WithScoring(input, input.ParameterSet.Scoring with { MinimumConfidence = (decimal)minimum });

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Confidence.Should().Be(0.931546m);
        result.BlockingReasons.Any(x => x.ReasonCode == MarketConditionReasonCodes.Confidence).Should().Be(blocked);
    }

    [Theory]
    [InlineData(IntrinsicTimeModeType.TrendDirectionChanged, 0.9, 0.1, MarketConditionPhase.Initiating)]
    [InlineData(IntrinsicTimeModeType.TrendReversalChanged, 0.9, 0.8, MarketConditionPhase.Reversing)]
    [InlineData(IntrinsicTimeModeType.HoldTradeChanged, 0.5, 0.70, MarketConditionPhase.Exhausting)]
    [InlineData(IntrinsicTimeModeType.HoldTradeChanged, 0.5, 0.40, MarketConditionPhase.Weakening)]
    [InlineData(IntrinsicTimeModeType.TrendExtremeChanged, 0.5, 0.39, MarketConditionPhase.Continuing)]
    [InlineData(IntrinsicTimeModeType.HoldTradeChanged, 1.0, 0.39, MarketConditionPhase.Confirmed)]
    [InlineData(IntrinsicTimeModeType.Trending, 0.5, 0.39, MarketConditionPhase.Confirmed)]
    [InlineData(IntrinsicTimeModeType.HoldTradeChanged, 0.5, 0.39, MarketConditionPhase.Undefined)]
    public void Phase_precedence_and_boundaries_are_frozen(IntrinsicTimeModeType mode,
        double band, double reversal, MarketConditionPhase expected)
    {
        var input = MarketConditionV1Tests.Healthy();
        input = input with { TriggerEvent = input.TriggerEvent with { FuturesItiSignal =
            input.TriggerEvent.FuturesItiSignal! with
                { IntrinsicTimeMode = mode, BandLevel = band, ReversalLevel = reversal } } };

        new MarketConditionCalculationModel().Calculate(input).Phase.Should().Be(expected);
    }

    [Fact]
    public void Penalty_caps_and_conflicting_evidence_are_exact_and_preserved()
    {
        var input = MarketConditionV1Tests.Healthy() with
        {
            OptionalMissingCategoryCount = 9,
            ConflictingEvidenceCount = 9,
            ConflictingEvidenceItems =
            [
                new() { Area = MarketConditionEvidenceArea.Classification, FeatureCode = "B", SourceId = "two" },
                new() { Area = MarketConditionEvidenceArea.Classification, FeatureCode = "A", SourceId = "one" }
            ]
        };

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.Confidence.Should().Be(0.581546m, "the combined penalty is capped at 0.35");
        result.ConflictingEvidenceItems.Select(x => x.FeatureCode).Should().Equal("A", "B");
        result.EvidenceItems.Count(x => x.ReasonCode == MarketConditionReasonCodes.OptionalMissing).Should().Be(9);
    }

    [Fact]
    public async Task Sequential_and_parallel_golden_vectors_are_byte_identical()
    {
        var model = new MarketConditionCalculationModel();
        var scenarios = new[] { "Bullish", "Bearish", "Range", "Transition", "Expansion", "Contraction" };
        var inputs = scenarios.ToDictionary(x => x, Scenario);
        var sequential = scenarios.ToDictionary(x => x,
            x => MessagePackSerializer.Serialize(model.Calculate(inputs[x])));
        var parallel = await Task.WhenAll(scenarios.Select(async name =>
            (name, bytes: await Task.Run(() => MessagePackSerializer.Serialize(model.Calculate(inputs[name]))))));

        parallel.Should().OnlyContain(x => x.bytes.SequenceEqual(sequential[x.name]));
    }

    [Fact]
    public void Result_round_trip_preserves_append_only_contract_and_invariants()
    {
        var input = MarketConditionV1Tests.Healthy();
        var result = new MarketConditionCalculationModel().Calculate(input);

        var roundTripped = MessagePackSerializer.Deserialize<MarketConditionResult>(
            MessagePackSerializer.Serialize(result));

        roundTripped.Should().BeEquivalentTo(result);
        var invalid = roundTripped with { Confidence = 2m };
        var action = () => MarketConditionCalculationModel.ValidateResult(invalid, input.ParameterSet);
        action.Should().Throw<MarketConditionCalculationException>()
            .Which.Category.Should().Be(MarketConditionFailureCategory.InvariantViolation);
    }

    static MarketConditionCalculationInput Scenario(string scenario)
    {
        var input = MarketConditionV1Tests.Healthy();
        var regime = input.RegimeResult;
        if (scenario == "Bearish")
        {
            input = input with { TriggerEvent = input.TriggerEvent with { FuturesItiSignal =
                input.TriggerEvent.FuturesItiSignal! with { IntrinsicTimeTrend = IntrinsicTimeTrendType.DownTrend } } };
            regime = regime with
            {
                Trend = regime.Trend with { Direction = RegimeDirection.Down },
                MarketStructure = regime.MarketStructure with { Direction = RegimeDirection.Down },
                Fusion = regime.Fusion with { Direction = RegimeDirection.Down }
            };
        }
        if (scenario is "Range" or "Transition" or "Contraction")
            regime = regime with
            {
                MarketStructure = regime.MarketStructure with
                {
                    Direction = RegimeDirection.Neutral,
                    Classification = scenario == "Transition" ? MarketStructureClassification.Transitioning :
                        scenario == "Contraction" ? MarketStructureClassification.Compressing :
                        MarketStructureClassification.Ranging
                },
                Fusion = regime.Fusion with
                {
                    Direction = RegimeDirection.Neutral,
                    Restrictions = scenario == "Transition" ? [RegimeRestriction.Transition] : []
                },
                Volatility = regime.Volatility with { Change = scenario == "Contraction"
                    ? VolatilityRegimeChange.Contracting : VolatilityRegimeChange.Stable }
            };
        if (scenario == "Expansion")
            regime = regime with { Volatility = regime.Volatility with { Change = VolatilityRegimeChange.Expanding } };
        return input with { RegimeResult = regime };
    }

    static MarketConditionCalculationInput WithScoring(MarketConditionCalculationInput input,
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
