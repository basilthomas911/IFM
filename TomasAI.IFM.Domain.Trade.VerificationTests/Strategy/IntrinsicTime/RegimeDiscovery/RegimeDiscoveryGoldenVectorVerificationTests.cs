using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

[Trait("Category", "Verification")]
public sealed class RegimeDiscoveryGoldenVectorVerificationTests
{
    public static TheoryData<TimeFrameType> SupportedHorizons => new()
    {
        TimeFrameType.Daily,
        TimeFrameType.Weekly,
        TimeFrameType.Monthly
    };

    /// <summary>Verifies exact golden scores with analytics freshness and authoritative trigger evidence.</summary>
    [Theory]
    [MemberData(nameof(SupportedHorizons))]
    public async Task Trending_up_is_the_same_authoritative_result_for_every_supported_horizon(
        TimeFrameType horizon)
    {
        var result = await CalculateAsync(RegimeDiscoveryScenarioCatalog.TrendingUp, horizon);

        result.TargetHorizon.Should().Be(horizon);
        result.MatchScenario(RegimeDiscoveryScenarioCatalog.TrendingUp);
        result.Trend.Confidence.Should().Be(0.982500m);
        result.Volatility.Confidence.Should().Be(0.959719m);
        // Trigger ITI freshness is 1.0, while cached analytics retain the fixture's 0.95.
        // Structure confidence increases by 0.35 * 0.15 * (1.0 - 0.95) = 0.002625.
        result.MarketStructure.Confidence.Should().Be(0.927525m);
        // Fusion rounds the 40/30/30 specialist confidence mix, then applies directional alignment.
        result.Decision.Confidence.Should().Be(0.939100m);
        result.OverallQuality.Should().Be(RegimeOverallQuality.High);
        result.SupportingEvidence.Select(value => value.TimeFrame).Distinct().Should()
            .BeSubsetOf(RegimeDiscoveryScenarioDataBuilder.CreateParameterSet(horizon)
                .Horizon.TimeFrames.Select(frame => frame.TimeFrame).Append(horizon));
    }

    [Fact]
    public async Task Bullish_breakout_is_not_misclassified_as_trending()
    {
        var result = await CalculateAsync(RegimeDiscoveryScenarioCatalog.BullishBreakout);

        result.MatchScenario(RegimeDiscoveryScenarioCatalog.BullishBreakout);
        result.MarketStructure.Classification.Should().NotBe(MarketStructureClassification.Trending);
    }

    [Theory]
    [MemberData(nameof(StructureScenarioData))]
    public async Task Extended_direction_and_structure_scenarios_match_their_business_contract(
        RegimeDiscoveryScenario scenario)
    {
        var result = await CalculateAsync(scenario);

        result.MatchScenario(scenario);
    }

    [Fact]
    public async Task Extreme_volatility_completes_with_no_new_trade_restriction()
    {
        var result = await CalculateAsync(RegimeDiscoveryScenarioCatalog.ExtremeVolatility);

        result.MatchScenario(RegimeDiscoveryScenarioCatalog.ExtremeVolatility);
        result.Reasons.Select(reason => reason.Code).Should().Contain(RegimeDiscoveryReasonCodes.VolatilityExtreme);
    }

    [Fact]
    public async Task Direction_conflict_is_preserved_as_a_fusion_restriction()
    {
        var result = await CalculateAsync(RegimeDiscoveryScenarioCatalog.DirectionConflict);

        result.MatchScenario(RegimeDiscoveryScenarioCatalog.DirectionConflict);
        result.Reasons.Select(reason => reason.Code).Should().Contain(RegimeDiscoveryReasonCodes.FusionDirectionConflict);
    }

    [Fact]
    public async Task Optional_realized_volatility_can_be_missing_without_failing_calculation()
    {
        var result = await CalculateAsync(RegimeDiscoveryScenarioCatalog.OptionalEvidenceMissing);

        result.MatchScenario(RegimeDiscoveryScenarioCatalog.OptionalEvidenceMissing);
        result.OverallQuality.Should().Be(RegimeOverallQuality.Degraded);
    }

    /// <summary>Requires complete golden results before comparing sequential and parallel serialization.</summary>
    [Fact]
    public async Task Sequential_and_parallel_golden_results_are_byte_equivalent()
    {
        var input = RegimeDiscoveryScenarioDataBuilder.CreateInput(
            RegimeDiscoveryScenarioCatalog.TrendingUp,
            TimeFrameType.Daily);
        var model = new RegimeDiscoveryCalculationModel();

        var sequential = await model.CalculateAsync(input, RegimeDiscoveryExecutionMode.Sequential);
        var parallel = await model.CalculateAsync(input, RegimeDiscoveryExecutionMode.ThreadPoolParallel);

        sequential.MatchScenario(RegimeDiscoveryScenarioCatalog.TrendingUp);
        parallel.MatchScenario(RegimeDiscoveryScenarioCatalog.TrendingUp);
        MessagePackSerializer.Serialize(parallel).Should().Equal(MessagePackSerializer.Serialize(sequential));
    }

    public static TheoryData<RegimeDiscoveryScenario> StructureScenarioData => new()
    {
        RegimeDiscoveryScenarioCatalog.TrendingDown,
        RegimeDiscoveryScenarioCatalog.RangeBound,
        RegimeDiscoveryScenarioCatalog.BullishBreakout,
        RegimeDiscoveryScenarioCatalog.BearishBreakout,
        RegimeDiscoveryScenarioCatalog.Compressing,
        RegimeDiscoveryScenarioCatalog.ExpandingUp,
        RegimeDiscoveryScenarioCatalog.Transitioning
    };

    static Task<RegimeDiscoveryResult> CalculateAsync(
        RegimeDiscoveryScenario scenario,
        TimeFrameType horizon = TimeFrameType.Daily) =>
        new RegimeDiscoveryCalculationModel().CalculateAsync(
            RegimeDiscoveryScenarioDataBuilder.CreateInput(scenario, horizon));
}
