using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

public sealed class FuturesItiOvershootTests
{
    static readonly DateOnly Date = new(2026, 11, 2);
    static readonly DateTime At = new(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc);

    public static IEnumerable<object[]> TrendsAndPeriods()
    {
        foreach (var period in new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly })
        foreach (var up in new[] { true, false })
            yield return [period, up];
    }

    public static IEnumerable<object[]> Boundaries()
    {
        foreach (var mode in new[] { IntrinsicTimeModeType.TrendExtremeChanged, IntrinsicTimeModeType.TrendReversalChanged })
        foreach (var level in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity, 0d, Math.BitDecrement(1d), 1d, Math.BitIncrement(1d), 1.4d })
            yield return [mode, level];
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void Only_finite_levels_strictly_above_one_select_overshoot(IntrinsicTimeModeType mode, double level)
    {
        var expected = double.IsFinite(level) && level > 1
            ? mode == IntrinsicTimeModeType.TrendExtremeChanged
                ? IntrinsicTimeModeType.TrendOvershootExtended : IntrinsicTimeModeType.TrendOvershootReversal
            : mode;
        Assert.Equal(expected, FuturesItiSignalCompute.ClassifyMode(mode, level));
    }

    [Theory]
    [InlineData(IntrinsicTimeModeType.TrendDirectionChanged)]
    [InlineData(IntrinsicTimeModeType.Trending)]
    [InlineData(IntrinsicTimeModeType.HoldTradeChanged)]
    public void Other_modes_keep_their_meaning_above_one(IntrinsicTimeModeType mode)
        => Assert.Equal(mode, FuturesItiSignalCompute.ClassifyMode(mode, 1.5));

    [Theory]
    [MemberData(nameof(TrendsAndPeriods))]
    public void Extension_pullback_new_extreme_and_return_below_one_preserve_the_current_group(TimeFrameType period, bool up)
    {
        var sign = up ? 1d : -1d;
        var start = Start(period, up);
        var ordinary = Next(start, start.TrendExtreme + sign * start.BandSize * 1.01);
        Assert.Equal(IntrinsicTimeModeType.TrendExtremeChanged, ordinary.IntrinsicTimeMode);
        Assert.True(ordinary.BandLevel < 1);

        var extended = Next(ordinary, start.TrendPrice + sign * start.Threshold * 1.4);
        Assert.Equal(IntrinsicTimeModeType.TrendOvershootExtended, extended.IntrinsicTimeMode);
        Assert.True(extended.BandLevel > 1);
        Assert.Equal(extended.IntrinsicPrice, extended.TrendExtreme);
        Assert.Equal(extended.TrendExtreme, extended.TrendReversal);
        Assert.Equal(0, extended.ReversalLevel);
        Assert.Equal(extended.Threshold, up ? extended.IntrinsicPrice - extended.DownTrendTrigger : extended.UpTrendTrigger - extended.IntrinsicPrice, 9);

        var pullback = Next(extended, extended.TrendReversal - sign * extended.BandSize * 1.01);
        Assert.Equal(IntrinsicTimeModeType.TrendOvershootReversal, pullback.IntrinsicTimeMode);
        Assert.True(pullback.BandLevel > 1);
        Assert.Equal(extended.TrendExtreme, pullback.TrendExtreme);
        Assert.Equal(pullback.IntrinsicPrice, pullback.TrendReversal);
        Assert.True(pullback.ReversalLevel > 0);
        Assert.Equal(extended.UpTrendTrigger, pullback.UpTrendTrigger);
        Assert.Equal(extended.DownTrendTrigger, pullback.DownTrendTrigger);

        var higherExtreme = Next(pullback, extended.TrendExtreme + sign * pullback.BandSize * 1.01);
        Assert.Equal(IntrinsicTimeModeType.TrendOvershootExtended, higherExtreme.IntrinsicTimeMode);
        Assert.Equal(higherExtreme.TrendExtreme, higherExtreme.TrendReversal);
        var secondPullback = Next(higherExtreme, higherExtreme.TrendReversal - sign * higherExtreme.BandSize * 1.01);
        Assert.Equal(IntrinsicTimeModeType.TrendOvershootReversal, secondPullback.IntrinsicTimeMode);
        Assert.True(sign * (secondPullback.IntrinsicPrice - pullback.IntrinsicPrice) > 0,
            "The new extreme resets reversal tracking even when this pullback has not passed the previous pullback.");

        var returned = Next(secondPullback, start.TrendPrice + sign * start.Threshold * 0.85);
        Assert.True(returned.BandLevel < 1);
        Assert.Equal(IntrinsicTimeModeType.TrendReversalChanged, returned.IntrinsicTimeMode);
        Assert.Equal(higherExtreme.TrendExtreme, returned.TrendExtreme);
        Assert.Equal(start.IntrinsicTimeGroupId, returned.IntrinsicTimeGroupId);

        var reentered = Next(returned, returned.TrendExtreme + sign * returned.BandSize * 1.01);
        Assert.Equal(IntrinsicTimeModeType.TrendOvershootExtended, reentered.IntrinsicTimeMode);
        Assert.True(reentered.BandLevel > 1);
        var changed = Next(reentered, up ? reentered.DownTrendTrigger : reentered.UpTrendTrigger);
        Assert.Equal(IntrinsicTimeModeType.TrendDirectionChanged, changed.IntrinsicTimeMode);
        Assert.Equal(start.IntrinsicTimeGroupId + 1, changed.IntrinsicTimeGroupId);
        Assert.Equal(0, changed.BandLevel);
    }

    [Theory]
    [MemberData(nameof(TrendsAndPeriods))]
    public void Exact_band_qualifies_but_inside_band_and_level_changes_alone_do_not(TimeFrameType period, bool up)
    {
        var start = Start(period, up);
        var boundary = start.TrendExtreme + (up ? start.BandSize : -start.BandSize);
        var inside = up ? Math.BitDecrement(boundary) : Math.BitIncrement(boundary);
        Assert.False(FuturesItiSignalCompute.TryCompute(Command(period, inside, 1), start, out _));
        Assert.True(FuturesItiSignalCompute.TryCompute(Command(period, boundary, 1), start, out var moved));
        Assert.Equal(IntrinsicTimeModeType.TrendExtremeChanged, moved.IntrinsicTimeMode);
        var levelOnly = Command(period, moved.IntrinsicPrice, 2) with { VixFuturesPrice = 1 };
        Assert.False(FuturesItiSignalCompute.TryCompute(levelOnly, moved, out _));
    }

    [Theory]
    [MemberData(nameof(TrendsAndPeriods))]
    public void Large_gap_emits_one_current_observation_and_rehydration_preserves_the_next_decision(TimeFrameType period, bool up)
    {
        var start = Start(period, up);
        var gap = Next(start, start.TrendPrice + (up ? 1 : -1) * start.Threshold * 1.4);
        var restored = MessagePackSerializer.Deserialize<FuturesItiSignalV2ReadModel>(MessagePackSerializer.Serialize(gap));
        Assert.Equal(gap, restored);
        var command = Command(period, gap.TrendReversal + (up ? -1 : 1) * gap.BandSize * 1.01, 3);
        Assert.True(FuturesItiSignalCompute.TryCompute(command, gap, out var uninterrupted));
        Assert.True(FuturesItiSignalCompute.TryCompute(command, restored, out var resumed));
        Assert.Equal(uninterrupted, resumed);
        Assert.Equal(IntrinsicTimeModeType.TrendOvershootReversal, resumed.IntrinsicTimeMode);
    }

    [Fact]
    public void Enum_wire_values_and_names_preserve_existing_contracts()
    {
        var names = new[] { "Trending", "TrendExtremeChanged", "TrendDirectionChanged", "TrendReversalChanged", "PredictedIntervalChanged", "HoldTradeChanged", "InTradeChanged", "TrendOvershootExtended", "TrendOvershootReversal" };
        for (var index = 0; index < names.Length; index++)
        {
            var mode = (IntrinsicTimeModeType)index;
            Assert.Equal(names[index], mode.ToStringFast());
            Assert.Equal(mode, MessagePackSerializer.Deserialize<IntrinsicTimeModeType>(MessagePackSerializer.Serialize(mode)));
        }
    }

    static GenerateFuturesItiSignalCommand Command(TimeFrameType period, double price, int seconds)
        => new("ES-OVERSHOOT", Date, period, At.AddSeconds(seconds), price, 20);

    static FuturesItiSignalV2ReadModel Start(TimeFrameType period, bool up)
    {
        Assert.True(FuturesItiSignalCompute.TryCompute(Command(period, 5000, 0), null, out var start));
        if (!up)
            start = Next(start, start.DownTrendTrigger);
        Assert.Equal(up ? IntrinsicTimeTrendType.UpTrend : IntrinsicTimeTrendType.DownTrend, start.IntrinsicTimeTrend);
        return start;
    }

    static FuturesItiSignalV2ReadModel Next(FuturesItiSignalV2ReadModel current, double price)
    {
        var command = Command(current.TimePeriod, price, 1) with { Timestamp = current.IntrinsicTime.AddSeconds(1) };
        Assert.True(FuturesItiSignalCompute.TryCompute(command, current, out var next));
        Assert.Equal(0.20, next.BandPercentage);
        Assert.Equal(next.Threshold * 0.20, next.BandSize);
        var progress = next.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend ? next.IntrinsicPrice - next.TrendPrice : next.TrendPrice - next.IntrinsicPrice;
        Assert.Equal(progress / next.Threshold, next.BandLevel);
        return next;
    }
}
