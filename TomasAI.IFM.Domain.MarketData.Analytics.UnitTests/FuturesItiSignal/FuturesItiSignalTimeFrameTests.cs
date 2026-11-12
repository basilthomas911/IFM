using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiSignalTimeFrameTests
{
    const string ContractId = "ES20260918";
    static readonly DateOnly Tuesday = new(2026, 9, 8);
    static readonly DateTime Timestamp = new(2026, 9, 8, 14, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TimeFrameType.Daily, "2026-09-08", "2026-09-08")]
    [InlineData(TimeFrameType.Weekly, "2026-09-02", "2026-09-08")]
    [InlineData(TimeFrameType.Monthly, "2026-09-01", "2026-09-08")]
    public void HistoryWindow_ResolvesCalendarDisplayPeriod(
        TimeFrameType timePeriod,
        string expectedStart,
        string expectedEnd)
    {
        var window = FuturesItiSignalHistoryWindow.Resolve(Tuesday, timePeriod);

        window.StartValueDate.Should().Be(DateOnly.Parse(expectedStart));
        window.EndValueDate.Should().Be(DateOnly.Parse(expectedEnd));
    }

    [Fact]
    public void HistoryWindow_RejectsUnsupportedTimeFrame()
        => FluentActions.Invoking(() => FuturesItiSignalHistoryWindow.Resolve(
                Tuesday,
                TimeFrameType.OneMinute))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Evaluator_DirectionIsImmediateAndOnlyDirectionIncrementsGroup()
    {
        var current = Signal(TimeFrameType.Daily, Tuesday, Tuesday, groupId: 3) with
        {
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            DownTrendTrigger = 4_990,
            TrendExtreme = 5_000,
            TrendReversal = 5_000,
            BandAnchorPrice = 5_000,
            BandSize = 2
        };
        var command = Command(TimeFrameType.Daily, 4_990);

        FuturesItiSignalCompute.TryCompute(command, current, out var changed)
            .Should().BeTrue();
        changed.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        changed.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);
        changed.IntrinsicTimeGroupId.Should().Be(4);
    }

    [Fact]
    public void Evaluator_ExtremeReversalAndTrendingRequireAFullBand()
    {
        var current = Signal(TimeFrameType.Daily, Tuesday, Tuesday, groupId: 2) with
        {
            IntrinsicTimeTrend = IntrinsicTimeTrendType.DownTrend,
            UpTrendTrigger = 5_100,
            TrendExtreme = 4_990,
            TrendReversal = 5_010,
            BandAnchorPrice = 5_000,
            BandSize = 2
        };

        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 5_001.99), current, out _)
            .Should().BeFalse();

        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 4_988), current, out var extreme)
            .Should().BeTrue();
        extreme.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendOvershootExtended);
        extreme.IntrinsicTimeGroupId.Should().Be(2);

        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 5_012), current, out var reversal)
            .Should().BeTrue();
        reversal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
        reversal.IntrinsicTimeGroupId.Should().Be(2);

        var rangeCurrent = current with
        {
            TrendExtreme = 4_900,
            TrendReversal = 5_100
        };
        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 5_002), rangeCurrent, out var trending)
            .Should().BeTrue();
        trending.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.Trending);
        trending.IntrinsicTimeGroupId.Should().Be(2);
    }

    [Theory]
    [InlineData(TimeFrameType.Daily, 1)]
    [InlineData(TimeFrameType.Weekly, 20)]
    [InlineData(TimeFrameType.Monthly, 60)]
    public void Evaluator_ThresholdChangesOnlyWhenDirectionChanges(TimeFrameType period, int tradingDays)
    {
        var lambda = 0.002 * Math.Sqrt(20.0 / 15.7);
        double BaseThreshold(double price, int days) => price * lambda + 0.25 * Math.Pow(days, 1.5);
        double UpThreshold(double price) => 5 * BaseThreshold(price, tradingDays) / BaseThreshold(price, 1);

        FuturesItiSignalCompute.TryCompute(Command(period, 5_000), null, out var started).Should().BeTrue();
        started.Threshold.Should().BeApproximately(UpThreshold(5_000), 1e-10);
        started.BandSize.Should().BeApproximately(UpThreshold(5_000) * 0.20, 1e-10);
        started.DownTrendTrigger.Should().BeApproximately(5_000 - UpThreshold(5_000), 1e-10);

        var downPrice = started.DownTrendTrigger;
        FuturesItiSignalCompute.TryCompute(Command(period, downPrice), started, out var down).Should().BeTrue();
        var expectedThreshold = BaseThreshold(downPrice, tradingDays) * 1.15;
        down.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        down.Threshold.Should().BeApproximately(expectedThreshold, 1e-10);
        down.BandSize.Should().BeApproximately(expectedThreshold * 0.20, 1e-10);
        down.UpTrendTrigger.Should().BeApproximately(downPrice + expectedThreshold, 1e-10);

        var newExtreme = down.TrendExtreme - down.BandSize * 1.01;
        FuturesItiSignalCompute.TryCompute(Command(period, newExtreme) with { VixFuturesPrice = 40 }, down, out var extended)
            .Should().BeTrue();
        extended.Threshold.Should().Be(down.Threshold);
        extended.BandSize.Should().Be(down.BandSize);
        extended.UpTrendTrigger.Should().BeApproximately(newExtreme + expectedThreshold, 1e-10);

        FuturesItiSignalCompute.TryCompute(Command(period, extended.UpTrendTrigger), extended, out var up).Should().BeTrue();
        up.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        up.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
        up.Threshold.Should().BeApproximately(UpThreshold(up.IntrinsicPrice), 1e-10);
        up.BandSize.Should().BeApproximately(up.Threshold * 0.20, 1e-10);
        up.DownTrendTrigger.Should().Be(up.IntrinsicPrice - up.Threshold);
    }

    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Evaluator_UptrendSuppressesOrdinaryEventsAndTrailsOnlyOnOvershoot(TimeFrameType period)
    {
        FuturesItiSignalCompute.TryCompute(Command(period, 5_000), null, out var started).Should().BeTrue();
        var threshold = started.Threshold;
        FuturesItiSignalCompute.TryCompute(Command(period, 5_000 + threshold * 0.99), started, out _).Should().BeFalse();
        FuturesItiSignalCompute.TryCompute(Command(period, 5_000 - started.BandSize * 1.01), started, out _).Should().BeFalse();
        FuturesItiSignalCompute.TryCompute(Command(period, started.DownTrendTrigger), started, out var down).Should().BeTrue();
        down.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);

        var high = 5_000 + threshold * 1.6;
        FuturesItiSignalCompute.TryCompute(Command(period, high) with { VixFuturesPrice = 40 }, started, out var extension)
            .Should().BeTrue();
        extension.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendOvershootExtended);
        extension.Threshold.Should().Be(threshold);
        extension.BandSize.Should().Be(started.BandSize);
        extension.DownTrendTrigger.Should().Be(high - threshold);
        FuturesItiSignalCompute.TryCompute(Command(period, 5_000 + threshold * 1.2), extension, out var reversal).Should().BeTrue();
        reversal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendOvershootReversal);
        reversal.Threshold.Should().Be(threshold);
        reversal.DownTrendTrigger.Should().Be(extension.DownTrendTrigger);
        FuturesItiSignalCompute.TryCompute(Command(period, high), reversal, out _).Should().BeFalse();
        FuturesItiSignalCompute.TryCompute(Command(period, 5_000 + threshold * 0.95), reversal, out _).Should().BeFalse();
        FuturesItiSignalCompute.TryCompute(Command(period, reversal.DownTrendTrigger), reversal, out down).Should().BeTrue();
        down.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);
    }

    [Theory]
    [InlineData(IntrinsicTimeTrendType.UpTrend, 5_010, 0.10)]
    [InlineData(IntrinsicTimeTrendType.UpTrend, 5_100, 1.00)]
    [InlineData(IntrinsicTimeTrendType.UpTrend, 5_125, 1.25)]
    [InlineData(IntrinsicTimeTrendType.UpTrend, 4_970, -0.30)]
    [InlineData(IntrinsicTimeTrendType.DownTrend, 4_990, 0.10)]
    [InlineData(IntrinsicTimeTrendType.DownTrend, 4_900, 1.00)]
    [InlineData(IntrinsicTimeTrendType.DownTrend, 4_875, 1.25)]
    [InlineData(IntrinsicTimeTrendType.DownTrend, 5_030, -0.30)]
    public void CalculateBandLevel_ReturnsSignedThresholdProgress(
        IntrinsicTimeTrendType trend,
        double price,
        double expected)
    {
        var signal = Signal(TimeFrameType.Daily, Tuesday, Tuesday, groupId: 1) with
        {
            IntrinsicTimeTrend = trend,
            TrendPrice = 5_000,
            IntrinsicPrice = price
        };

        FuturesItiSignalCompute.CalculateBandLevel(signal, threshold: 100)
            .Should().BeApproximately(expected, 1e-10);
    }

    [Theory]
    [InlineData(IntrinsicTimeTrendType.UpTrend, 5_120, 5_120, 0.00)]
    [InlineData(IntrinsicTimeTrendType.UpTrend, 5_120, 5_060, 0.50)]
    [InlineData(IntrinsicTimeTrendType.UpTrend, 5_120, 5_000, 1.00)]
    [InlineData(IntrinsicTimeTrendType.UpTrend, 5_120, 4_970, 1.25)]
    [InlineData(IntrinsicTimeTrendType.DownTrend, 4_880, 4_880, 0.00)]
    [InlineData(IntrinsicTimeTrendType.DownTrend, 4_880, 4_940, 0.50)]
    [InlineData(IntrinsicTimeTrendType.DownTrend, 4_880, 5_000, 1.00)]
    [InlineData(IntrinsicTimeTrendType.DownTrend, 4_880, 5_030, 1.25)]
    public void CalculateReversalLevel_ReturnsEstablishedMoveRetracement(
        IntrinsicTimeTrendType trend,
        double extreme,
        double price,
        double expected)
    {
        var signal = Signal(TimeFrameType.Daily, Tuesday, Tuesday, groupId: 1) with
        {
            IntrinsicTimeTrend = trend,
            TrendPrice = 5_000,
            TrendExtreme = extreme,
            IntrinsicPrice = price
        };

        FuturesItiSignalCompute.CalculateReversalLevel(signal)
            .Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void CalculateLevels_ZeroDenominatorsReturnZero()
    {
        var signal = Signal(TimeFrameType.Daily, Tuesday, Tuesday, groupId: 0) with
        {
            TrendPrice = 5_000,
            TrendExtreme = 5_000,
            IntrinsicPrice = 5_000
        };

        FuturesItiSignalCompute.CalculateBandLevel(signal, threshold: 0).Should().Be(0);
        FuturesItiSignalCompute.CalculateReversalLevel(signal).Should().Be(0);
    }

    [Theory]
    [InlineData(typeof(GenerateFuturesItiSignalCommand), nameof(GenerateFuturesItiSignalCommand.TimeFrameStartValueDate), 12)]
    [InlineData(typeof(FuturesItiSignalGeneratedEvent), nameof(FuturesItiSignalGeneratedEvent.DeriveLongerPeriods), 12)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.TimeFrameStartValueDate), 21)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.BandAnchorPrice), 22)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.BandPercentage), 23)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.BandSize), 24)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.BandLevel), 25)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.ReversalLevel), 26)]
    public void MessagePackContracts_PreserveEstablishedAndAdditiveKeys(
        Type contractType,
        string propertyName,
        int expectedKey)
    {
        var key = contractType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?.GetCustomAttribute<KeyAttribute>();

        key.Should().NotBeNull();
        key!.IntKey.Should().Be(expectedKey);
    }

    static GenerateFuturesItiSignalCommand Command(TimeFrameType period, double price)
        => new(ContractId, Tuesday, period, Timestamp.AddSeconds(1), price, 20, Tuesday);

    static FuturesItiSignalV2ReadModel Signal(
        TimeFrameType period,
        DateOnly valueDate,
        DateOnly frameStart,
        int groupId)
        => new(
            ContractId,
            valueDate,
            period,
            10,
            Timestamp,
            groupId,
            0,
            5_000,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeModeType.Trending,
            5_000,
            5_000,
            5_000,
            0,
            0,
            0.003,
            period == TimeFrameType.Daily ? 1 : period == TimeFrameType.Weekly ? 20 : 60,
            10,
            5_000,
            4_990,
            IntrinsicTimeTradeState.Ready,
            frameStart,
            5_000,
            0.20,
            2);

}
