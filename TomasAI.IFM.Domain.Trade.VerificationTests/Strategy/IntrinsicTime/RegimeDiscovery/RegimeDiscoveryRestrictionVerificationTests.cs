using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

[Trait("Category", "Verification")]
public sealed class RegimeDiscoveryRestrictionVerificationTests
{
    [Fact]
    public async Task Severe_backwardation_and_extreme_volatility_expand_and_restrict_new_trades()
    {
        var result = await CalculateAsync(RegimeDiscoveryScenarioCatalog.ExtremeVolatility);

        result.Volatility.Level.Should().Be(VolatilityRegimeLevel.Extreme);
        result.Volatility.Change.Should().Be(VolatilityRegimeChange.Expanding);
        result.Volatility.TermStructure.Should().Be(VxTermStructureRegime.Backwardation);
        result.Volatility.NoNewTrade.Should().BeTrue();
        result.Fusion.Restrictions.Should().ContainSingle().Which.Should().Be(RegimeRestriction.NoNewTrade);
    }

    [Fact]
    public async Task Lower_atr_compression_contracts_volatility()
    {
        var result = await CalculateAsync(RegimeDiscoveryScenarioCatalog.Compressing);

        result.Volatility.Level.Should().Be(VolatilityRegimeLevel.Low);
        result.Volatility.Change.Should().Be(VolatilityRegimeChange.Contracting);
        result.Reasons.Select(reason => reason.Code).Should()
            .Contain(RegimeDiscoveryReasonCodes.VolatilityContracting);
    }

    [Fact]
    public void Complete_low_confidence_specialists_create_an_explicit_fusion_restriction()
    {
        var result = new MarketRegimeFusionModel().Calculate(
            new TrendRegimeResult
            {
                IsComplete = true,
                Direction = RegimeDirection.Up,
                Score = 0.25m,
                Confidence = 0.20m
            },
            new VolatilityRegimeResult
            {
                IsComplete = true,
                Level = VolatilityRegimeLevel.Normal,
                Score = 0.35m,
                Confidence = 0.20m
            },
            new MarketStructureRegimeResult
            {
                IsComplete = true,
                Classification = MarketStructureClassification.Trending,
                Direction = RegimeDirection.Up,
                Score = 0.25m,
                Confidence = 0.20m
            },
            new MarketRegimeFusionConfiguration());

        result.IsComplete.Should().BeTrue();
        result.Confidence.Should().Be(0.20m);
        result.Quality.Should().Be(RegimeOverallQuality.Low);
        result.Restrictions.Should().ContainSingle().Which.Should().Be(RegimeRestriction.LowConfidence);
        result.Reasons.Select(reason => reason.Code).Should().Contain(RegimeDiscoveryReasonCodes.FusionLowConfidence);
    }

    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Scenario_builder_populates_every_snapshot_requirement(TimeFrameType horizon)
    {
        var input = RegimeDiscoveryScenarioDataBuilder.CreateInput(
            RegimeDiscoveryScenarioCatalog.TrendingUp, horizon);
        var requirements = RegimeDiscoverySnapshotRequestFactory.Create(
            input.Snapshot.MarketSeriesIdentity,
            input.ParameterSet).Requirements;

        input.Snapshot.Observations.Select(value => (value.Metric, value.SignalKey.TimeFrame)).Should()
            .BeEquivalentTo(requirements.Select(value => (value.Metric, value.TimeFrame)));
    }

    static Task<RegimeDiscoveryResult> CalculateAsync(RegimeDiscoveryScenario scenario) =>
        new RegimeDiscoveryCalculationModel().CalculateAsync(
            RegimeDiscoveryScenarioDataBuilder.CreateInput(scenario, TimeFrameType.Daily));
}
