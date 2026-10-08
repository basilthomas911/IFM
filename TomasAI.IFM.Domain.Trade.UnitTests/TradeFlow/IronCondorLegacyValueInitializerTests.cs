using FluentAssertions;
using Xunit;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorLegacyValueInitializerTests
{
    [Fact]
    public void Five_day_ema_seeds_from_five_daily_closes_then_uses_one_third_alpha()
    {
        IronCondorLegacyValueInitializers.CalculateFiveDayXma([10m, 20m, 30m, 40m, 50m]).Should().Be(30);
        IronCondorLegacyValueInitializers.CalculateFiveDayXma([10m, 20m, 30m, 40m, 50m, 60m, 70m]).Should().BeApproximately(50, 1e-12);
        FluentActions.Invoking(() => IronCondorLegacyValueInitializers.CalculateFiveDayXma([10m, 20m, 30m, 40m]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => IronCondorLegacyValueInitializers.CalculateFiveDayXma([10m, 20m, 0m, 40m, 50m]))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1, 0.04)]
    [InlineData(10, 0.04)]
    [InlineData(-10, -0.04)]
    public void Forward_delta_is_signed_per_strategy_unit_and_quantity_does_not_scale_it(int quantity, double expected)
        => IronCondorLegacyValueInitializers.CalculateForwardDelta(
            [(-quantity, 0.20), (quantity, 0.09), (-quantity, -0.22), (quantity, -0.07)])
            .Should().BeApproximately(expected, 1e-12);

    [Fact]
    public void Forward_delta_rejects_missing_legs_unbalanced_quantities_and_nonfinite_valuations()
    {
        FluentActions.Invoking(() => IronCondorLegacyValueInitializers.CalculateForwardDelta([(-1, 0.2)]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => IronCondorLegacyValueInitializers.CalculateForwardDelta([(-1, 0.2), (1, 0.1), (-2, -0.2), (1, -0.1)]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => IronCondorLegacyValueInitializers.CalculateForwardDelta([(-1, 0.2), (1, double.NaN), (-1, -0.2), (1, -0.1)]))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(TradeType.ShortIronCondor, 0.8, false, TradeRiskType.High)]
    [InlineData(TradeType.ShortIronCondor, 0.7, true, TradeRiskType.Critical)]
    [InlineData(TradeType.LongIronCondor, 0.8, true, TradeRiskType.Low)]
    [InlineData(TradeType.LongIronCondor, 0.6, false, TradeRiskType.High)]
    public void Trade_risk_preserves_strategy_and_critical_bands(TradeType strategy, double score, bool critical, TradeRiskType risk)
        => IronCondorLegacyValueInitializers.CalculateTradeRisk(strategy, score, critical).Should().Be(risk);

    [Fact]
    public void Forward_prices_preserve_legacy_absolute_sum_and_price_limit_units()
    {
        IronCondorLegacyValueInitializers.CalculateForwardPrice(-12m, 8m).Should().Be(20m);
        IronCondorLegacyValueInitializers.CalculateForwardLossRatio(20m, 25m).Should().Be(0.8);
        var invalid = () => IronCondorLegacyValueInitializers.CalculateForwardLossRatio(0m, 0m);
        invalid.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Mscore_preserves_even_sample_lower_median_and_does_not_modify_history()
    {
        double[] history = [1, 4, 9];
        // Transformed sample [1,2,3,4]: legacy median 2; deviations [1,0,1,2]: MAD 1.
        IronCondorLegacyValueInitializers.CalculateMScore(16, history).Should().BeApproximately(4 / 5.5, 1e-12);
        history.Should().Equal(1, 4, 9);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void Mscore_rejects_invalid_inputs(double ratio)
    {
        var calculate = () => IronCondorLegacyValueInitializers.CalculateMScore(ratio, new double[] { 1 });
        calculate.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Mscore_rejects_missing_history_and_zero_normalization()
    {
        var missing = () => IronCondorLegacyValueInitializers.CalculateMScore(1, Array.Empty<double>());
        var zero = () => IronCondorLegacyValueInitializers.CalculateMScore(0, new double[] { 0 });
        missing.Should().Throw<ArgumentException>();
        zero.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(TradeType.ShortIronCondor, -1, 0.70, 0.73)]
    [InlineData(TradeType.ShortIronCondor, 59.9, 0.75, 0.78)]
    [InlineData(TradeType.ShortIronCondor, 1000, 0.80, 0.83)]
    [InlineData(TradeType.LongIronCondor, 0, 0.52, 0.49)]
    [InlineData(TradeType.LongIronCondor, 100, 0.62, 0.59)]
    public void Mdi_mapping_preserves_strategy_bands(TradeType strategy, double mdi, double warning, double limit)
    {
        IronCondorLegacyValueInitializers.CalculateMdiWarningRatio(strategy, mdi).Should().Be(warning);
        IronCondorLegacyValueInitializers.CalculateMdiLimitRatio(strategy, mdi).Should().Be(limit);
    }

    [Theory]
    [InlineData(0.00100, 0.00119, GammaRiskType.None)]
    [InlineData(0.00100, 0.00120, GammaRiskType.LowShortCallGamma)]
    [InlineData(0.00100, 0.00130, GammaRiskType.HighShortCallGamma)]
    [InlineData(0, 0.00130, GammaRiskType.None)]
    public void Gamma_classification_preserves_boundaries_and_legacy_names(double call, double put, GammaRiskType risk)
        => IronCondorLegacyValueInitializers.CalculateGammaRisk(call, put).Should().Be(risk);

    [Fact]
    public void Mdi_and_trailing_thresholds_preserve_price_and_currency_units()
    {
        IronCondorLegacyValueInitializers.CalculateMdiPriceThreshold(0.75, -10m).Should().Be(15m);
        IronCondorLegacyValueInitializers.CalculateNextTrailingStopRatio(0).Should().Be(0.20);
        IronCondorLegacyValueInitializers.CalculateNextTrailingStopRatio(0.20).Should().Be(0.25);
        IronCondorLegacyValueInitializers.CalculateTrailingStopThreshold(0.25, 1000m).Should().Be(200m);
    }
}
