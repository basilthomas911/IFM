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
    [InlineData(TimeFrameType.Monthly, "2026-08-08", "2026-09-08")]
    public void HistoryWindow_ResolvesTrailingDisplayPeriod(
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
            BandSize = 1
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
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            DownTrendTrigger = 4_900,
            TrendExtreme = 5_010,
            TrendReversal = 4_990,
            BandAnchorPrice = 5_000,
            BandSize = 2
        };

        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 5_001.99), current, out _)
            .Should().BeFalse();

        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 5_012), current, out var extreme)
            .Should().BeTrue();
        extreme.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendExtremeChanged);
        extreme.IntrinsicTimeGroupId.Should().Be(2);

        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 4_988), current, out var reversal)
            .Should().BeTrue();
        reversal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
        reversal.IntrinsicTimeGroupId.Should().Be(2);

        var rangeCurrent = current with
        {
            TrendExtreme = 5_100,
            TrendReversal = 4_900
        };
        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 5_002), rangeCurrent, out var trending)
            .Should().BeTrue();
        trending.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.Trending);
        trending.IntrinsicTimeGroupId.Should().Be(2);
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
            period == TimeFrameType.Daily ? 1 : period == TimeFrameType.Weekly ? 10 : 30,
            10,
            5_000,
            4_990,
            IntrinsicTimeTradeState.Ready,
            frameStart,
            5_000,
            0.10,
            1);

}
