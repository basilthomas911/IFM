using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class FuturesSessionAccumulatorTests
{
    private static readonly DateOnly ValueDate = new(2026, 8, 18);

    [Fact]
    public void Replay_volume_is_not_complete_until_the_boundary_marker()
    {
        var accumulator = new FuturesSessionAccumulator();

        Assert.True(accumulator.TryAccumulateTrade(
            "ESU6", ValueDate, Trade(10, 125), replay: true, out var bootstrapping));
        Assert.Equal(125, bootstrapping.Volume);
        Assert.Equal(FuturesSessionVolumeQuality.Bootstrapping, bootstrapping.VolumeQuality);
        Assert.False(bootstrapping.HasVolume);

        var completed = accumulator.CompleteTradeReplay("ESU6", ValueDate);

        Assert.Equal(125, completed.Volume);
        Assert.Equal(FuturesSessionVolumeQuality.ObservedComplete, completed.VolumeQuality);
        Assert.True(completed.HasVolume);
    }

    [Fact]
    public void Replay_and_live_boundary_is_idempotent_and_supports_bigint_volume()
    {
        var accumulator = new FuturesSessionAccumulator();
        var firstSize = uint.MaxValue;

        Assert.True(accumulator.TryAccumulateTrade(
            "VXU6", ValueDate, Trade(10, firstSize), replay: true, out _));
        Assert.False(accumulator.TryAccumulateTrade(
            "VXU6", ValueDate, Trade(10, firstSize), replay: true, out _));
        _ = accumulator.CompleteTradeReplay("VXU6", ValueDate);
        Assert.True(accumulator.TryAccumulateTrade(
            "VXU6", ValueDate, Trade(11, 25), replay: false, out var current));

        Assert.Equal((long)uint.MaxValue + 25L, current.Volume);
        Assert.True(current.Volume > int.MaxValue);
        Assert.Equal(FuturesSessionVolumeQuality.ObservedComplete, current.VolumeQuality);
    }

    [Fact]
    public void Official_cleared_volume_replaces_observed_volume_and_is_final()
    {
        var accumulator = new FuturesSessionAccumulator();
        Assert.True(accumulator.TryAccumulateTrade(
            "ESU6", ValueDate, Trade(1, 100), replay: false, out _));

        Assert.False(accumulator.TryApplyStatistic(
            "ESU6",
            ValueDate,
            Statistic(2, statisticType: 6, quantity: long.MaxValue, referenceDate: ValueDate),
            out _));

        Assert.True(accumulator.TryApplyStatistic(
            "ESU6",
            ValueDate,
            Statistic(3, statisticType: 6, quantity: 12_345, referenceDate: ValueDate),
            out var official));
        Assert.Equal(12_345, official.Volume);
        Assert.Equal(FuturesSessionVolumeQuality.OfficialFinal, official.VolumeQuality);

        Assert.False(accumulator.TryAccumulateTrade(
            "ESU6", ValueDate, Trade(4, 50), replay: false, out var unchanged));
        Assert.Equal(12_345, unchanged.Volume);
    }

    [Fact]
    public void Official_volume_uses_the_statistics_reference_date()
    {
        var accumulator = new FuturesSessionAccumulator();
        var priorDate = ValueDate.AddDays(-1);

        Assert.True(accumulator.TryApplyStatistic(
            "ESU6",
            ValueDate,
            Statistic(4, statisticType: 6, quantity: 9_876, referenceDate: priorDate),
            out var snapshot));

        Assert.Equal(priorDate, snapshot.ValueDate);
        Assert.Equal(9_876, snapshot.Volume);
        Assert.False(accumulator.TryRead("ESU6", ValueDate, out _));
        Assert.True(accumulator.TryRead("ESU6", priorDate, out var prior));
        Assert.Equal(FuturesSessionVolumeQuality.OfficialFinal, prior.VolumeQuality);
    }

    [Fact]
    public void Open_high_low_and_volume_can_complete_independently()
    {
        var accumulator = new FuturesSessionAccumulator();
        Assert.False(accumulator.TryApplyStatistic(
            "ESU6", ValueDate, PriceStatistic(1, 1, 6_500m), out _));
        Assert.False(accumulator.TryApplyStatistic(
            "ESU6", ValueDate, PriceStatistic(2, 4, 6_450m), out _));
        Assert.True(accumulator.TryApplyStatistic(
            "ESU6", ValueDate, PriceStatistic(3, 5, 6_525m), out var prices));
        Assert.True(prices.HasPriceStatistics);
        Assert.False(prices.HasVolume);

        Assert.True(accumulator.TryApplyStatistic(
            "ESU6",
            ValueDate,
            Statistic(4, statisticType: 6, quantity: 100_000, referenceDate: ValueDate),
            out var combined));
        Assert.True(combined.HasPriceStatistics);
        Assert.True(combined.HasVolume);
    }

    private static TradeRecord64 Trade(uint sequence, uint size) => new(
        new MarketRecordHeader32(
            42, 7, MarketRecordKind.Trade, 0, sequence, sequence, sequence),
        6_500_000_000_000,
        size,
        (byte)'T',
        (byte)'N',
        0);

    private static StatisticsRecord64 PriceStatistic(
        uint sequence,
        ushort statisticType,
        decimal price) => new(
        new MarketRecordHeader32(
            42, 7, MarketRecordKind.Statistics, 0, sequence, sequence, sequence),
        decimal.ToInt64(price * 1_000_000_000m),
        0,
        0,
        statisticType,
        0,
        1,
        0);

    private static StatisticsRecord64 Statistic(
        uint sequence,
        ushort statisticType,
        long quantity,
        DateOnly referenceDate)
    {
        var reference = new DateTimeOffset(
            referenceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var referenceNanoseconds = checked(
            (reference.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100L);
        return new StatisticsRecord64(
            new MarketRecordHeader32(
                42, 7, MarketRecordKind.Statistics, 0, sequence, sequence, sequence),
            0,
            quantity,
            referenceNanoseconds,
            statisticType,
            0,
            1,
            0);
    }
}
