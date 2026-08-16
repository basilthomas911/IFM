using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public sealed class FuturesTdiSignalComputeTests
{
    [Fact]
    public void Create_StandardConfiguration_ComputesOriginalTdiLinesAndBands()
    {
        var signals = CreateRsiSeries(40d, 34);

        var created = FuturesTdiSignalCompute.Create(
            signals,
            previous: null,
            FuturesTdiConfiguration.Standard,
            out var result);

        created.Should().BeTrue();
        result.Should().NotBeNull();
        result!.PriceLine.Should().BeApproximately(72.5d, 1e-12);
        result.SignalLine.Should().BeApproximately(70d, 1e-12);
        result.MarketBaseLine.Should().BeApproximately(56.5d, 1e-12);
        var expectedOffset = 1.6185d * Math.Sqrt(96.25d);
        result.UpperVolatilityBand.Should().BeApproximately(56.5d + expectedOffset, 1e-12);
        result.LowerVolatilityBand.Should().BeApproximately(56.5d - expectedOffset, 1e-12);
        result.Cross.Should().Be(FuturesTdiCrossType.None);
        result.MarketState.Should().Be(FuturesTdiMarketStateType.Overbought);
        result.TrendDirection.Should().Be(FuturesTrendDirectionType.UpTrending);
        result.TrendStrength.Should().Be(FuturesTrendDirectionStrengthType.High);
    }

    [Fact]
    public void Create_WithPreviousNegativeDivergence_DetectsBullishCross()
    {
        var previous = new FuturesTdiSignalReadModel(
            "ESU25",
            new DateOnly(2025, 6, 20),
            TimeFrameType.OneMinute,
            new TimeOnly(9, 59),
            FuturesTdiConfiguration.Standard,
            5500m,
            50d,
            49d,
            51d,
            50d,
            60d,
            40d,
            FuturesTrendDirectionType.DownTrending,
            FuturesTrendDirectionStrengthType.Medium,
            FuturesTdiCrossType.None,
            FuturesTdiMarketStateType.BelowMidline);

        var created = FuturesTdiSignalCompute.Create(
            CreateRsiSeries(40d, 34),
            previous,
            FuturesTdiConfiguration.Standard,
            out var result);

        created.Should().BeTrue();
        result!.Cross.Should().Be(FuturesTdiCrossType.Bullish);
        result.TrendDirection.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    [Fact]
    public void Create_InsufficientRsiWindow_DoesNotProducePartialTdi()
    {
        var created = FuturesTdiSignalCompute.Create(
            CreateRsiSeries(40d, 33),
            previous: null,
            FuturesTdiConfiguration.Standard,
            out var result);

        created.Should().BeFalse();
        result.Should().BeNull();
    }

    static FuturesRsiSignalReadModel[] CreateRsiSeries(double initialRsi, int count)
        => Enumerable.Range(0, count)
            .Select(index => new FuturesRsiSignalReadModel(
                "ESU25",
                new DateOnly(2025, 6, 20),
                TimeFrameType.OneMinute,
                13,
                new TimeOnly(9, 30).AddMinutes(index),
                5500m + index,
                1m,
                1m,
                0m,
                1m,
                0.5m,
                2d,
                initialRsi + index,
                0d,
                1d,
                index + 1,
                new DateTime(2025, 6, 20, 9, 30, 0).AddMinutes(index)))
            .ToArray();
}
