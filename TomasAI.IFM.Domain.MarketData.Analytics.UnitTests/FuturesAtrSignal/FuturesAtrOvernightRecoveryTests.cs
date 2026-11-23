using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAtrSignal;

public sealed class FuturesAtrOvernightRecoveryTests
{
    static readonly DateOnly Date = new(2026, 11, 19);
    static readonly DateTimeOffset Open = new(2026, 11, 19, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Overnight_seed_is_ready_before_open_and_restart_replays_only_missing_bars()
    {
        var overnight = Bars(Open.AddMinutes(-5));
        var state = new FuturesAtrSignalCommandState();
        Assert.True(Start(overnight).Execute(state).Success);
        Assert.True(state.AtrSignal!.IsWarm);
        Assert.True(state.AtrSignal.AtrBaseline > 0);
        Assert.True(state.AtrSignal.AtrRatio > 0);
        Assert.Equal(Open.AddMinutes(-5), state.FuturesAtrCheckpoint!.LastIntervalEndUtc);

        var restarted = new FuturesAtrSignalCommandState();
        foreach (var fact in state.Events)
        {
            if (fact is FuturesAtrSignalStartedEvent started)
                restarted.Apply(MessagePackSerializer.Deserialize<FuturesAtrSignalStartedEvent>(MessagePackSerializer.Serialize(started)), false);
            else if (fact is FuturesAtrSignalGeneratedEvent generated)
                restarted.Apply(MessagePackSerializer.Deserialize<FuturesAtrSignalGeneratedEvent>(MessagePackSerializer.Serialize(generated)), false);
        }
        var movingWindow = Bars(Open);
        Assert.True(Start(movingWindow).Execute(restarted).Success);
        Assert.False(restarted.Events.OfType<FuturesAtrSignalStartedEvent>().Single().ResetForHistoricalSeed);
        Assert.Single(restarted.Events.OfType<FuturesAtrSignalGeneratedEvent>());
        Assert.Equal(49, restarted.FuturesAtrCheckpoint!.ObservationCount);
        Assert.Equal(Open, restarted.FuturesAtrCheckpoint.LastIntervalEndUtc);
        Assert.True(restarted.AtrSignal!.AtrBaseline > 0);

        var expected = new FuturesAtrSignalCommandState();
        Assert.True(Start([.. overnight, movingWindow[^1]]).Execute(expected).Success);
        Assert.Equal(expected.AtrSignal!.AtrValue, restarted.AtrSignal.AtrValue);
        Assert.Equal(expected.AtrSignal.AtrBaseline, restarted.AtrSignal.AtrBaseline);
        Assert.Equal(expected.AtrSignal.AtrRatio, restarted.AtrSignal.AtrRatio);
        var count = restarted.FuturesAtrCheckpoint.ObservationCount;
        Assert.True(Start(overnight).Execute(restarted).Success); // delayed retry must not rewind live state
        Assert.Equal(count, restarted.FuturesAtrCheckpoint.ObservationCount);
        Assert.Equal(Open, restarted.FuturesAtrCheckpoint.LastIntervalEndUtc);
    }

    [Fact]
    public void Fourteen_bars_do_not_qualify_the_prior_only_baseline()
    {
        var state = new FuturesAtrSignalCommandState();
        Assert.False(Start(Bars(Open)[^14..]).Execute(state).Success);
        Assert.Empty(state.Events);
    }

    static StartFuturesAtrSignalCommand Start(FuturesTradeSessionBarReadModel[] bars)
        => new(new("ES-OVERNIGHT", Date, TimeFrameType.FiveMinutes, 14))
        { CommandId = Guid.NewGuid(), HistoricalSeed = bars };

    static FuturesTradeSessionBarReadModel[] Bars(DateTimeOffset cutoff)
    {
        var series = MarketSeriesIdentity.ForContract("ES-OVERNIGHT");
        return RsiHistoricalSeedWindowModel.Create(TimeFrameType.FiveMinutes, 48, cutoff, new CmeFuturesMarketSessionCalendar())
            .Select(window => new FuturesTradeSessionBarReadModel
            {
                ContractId = "ES-OVERNIGHT", ValueDate = window.ValueDate, TimeFrame = TimeFrameType.FiveMinutes,
                MarketSeriesIdentity = series,
                ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.FiveMinutes, window.EndUtc, window.EndUtc.UtcTicks),
                IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
                FirstMarketEventUtc = window.StartUtc.AddSeconds(1), LastMarketEventUtc = window.EndUtc.AddSeconds(-1),
                FirstSourceSequence = window.EndUtc.UtcTicks, LastSourceSequence = window.EndUtc.UtcTicks,
                Open = 6000, High = 6003 + window.EndUtc.Minute % 7, Low = 5998, Close = 6001,
                Volume = 100, PriceVolumeSum = 600100, TradeCount = 10,
                IsComplete = true, IsValid = true, SchemaVersion = 1, CalculationVersion = "overnight-test",
                CalculationMethod = MarketSignalCalculationMethod.ClosedObservation, CalculatedAtUtc = window.EndUtc
            }).ToArray();
    }
}
