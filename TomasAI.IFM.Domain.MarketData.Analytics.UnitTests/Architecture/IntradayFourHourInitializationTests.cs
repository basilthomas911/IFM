using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Architecture;

public sealed class IntradayFourHourInitializationTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 25);
    static readonly DateTimeOffset Cutoff = new(2026, 8, 25, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Replay_initializes_and_rehydrates_all_three_warm_accumulators()
    {
        var bars = Bars();
        var adxId = new FuturesAdxSignalEntityId("ES-SEED", ValueDate, TimeFrameType.FiveMinutes, 14);
        var atrId = new FuturesAtrSignalEntityId("ES-SEED", ValueDate, TimeFrameType.FiveMinutes, 14);
        var macdId = new FuturesMacdSignalEntityId("ES-SEED", ValueDate, TimeFrameType.FiveMinutes);

        var adxCommand = MessagePackSerializer.Deserialize<StartFuturesAdxSignalCommand>(
            MessagePackSerializer.Serialize(new StartFuturesAdxSignalCommand(adxId)
                { CommandId = Guid.NewGuid(), HistoricalSeed = bars }));
        var atrCommand = MessagePackSerializer.Deserialize<StartFuturesAtrSignalCommand>(
            MessagePackSerializer.Serialize(new StartFuturesAtrSignalCommand(atrId)
                { CommandId = Guid.NewGuid(), HistoricalSeed = bars }));
        var macdCommand = MessagePackSerializer.Deserialize<StartFuturesMacdSignalCommand>(
            MessagePackSerializer.Serialize(new StartFuturesMacdSignalCommand(macdId)
                { CommandId = Guid.NewGuid(), HistoricalSeed = bars }));

        var adx = new FuturesAdxSignalCommandState();
        var atr = new FuturesAtrSignalCommandState();
        var macd = new FuturesMacdSignalCommandState();
        Assert.True(adxCommand.Execute(adx).Success);
        Assert.True(atrCommand.Execute(atr).Success);
        Assert.True(macdCommand.Execute(macd).Success);
        Assert.All(adx.Events, e => Assert.Equal(adxCommand.CommandId, e.CommandId));
        Assert.All(atr.Events, e => Assert.Equal(atrCommand.CommandId, e.CommandId));
        Assert.All(macd.Events, e => Assert.Equal(macdCommand.CommandId, e.CommandId));
        Assert.Equal(48, adx.Events.OfType<FuturesAdxSignalGeneratedEvent>().Count());
        Assert.Equal(48, atr.Events.OfType<FuturesAtrSignalGeneratedEvent>().Count());
        Assert.Equal(48, macd.Events.OfType<FuturesMacdSignalGeneratedEvent>().Count());
        Assert.True(MessagePackSerializer.Deserialize<FuturesAdxSignalStartedEvent>(MessagePackSerializer.Serialize(
            Assert.Single(adx.Events.OfType<FuturesAdxSignalStartedEvent>()))).ResetForHistoricalSeed);
        Assert.True(MessagePackSerializer.Deserialize<FuturesAtrSignalStartedEvent>(MessagePackSerializer.Serialize(
            Assert.Single(atr.Events.OfType<FuturesAtrSignalStartedEvent>()))).ResetForHistoricalSeed);
        Assert.True(MessagePackSerializer.Deserialize<FuturesMacdSignalStartedEvent>(MessagePackSerializer.Serialize(
            Assert.Single(macd.Events.OfType<FuturesMacdSignalStartedEvent>()))).ResetForHistoricalSeed);

        var restoredAdx = new FuturesAdxSignalCommandState();
        foreach (var fact in adx.Events) restoredAdx.Apply(fact, false);
        var restoredAtr = new FuturesAtrSignalCommandState();
        foreach (var fact in atr.Events) restoredAtr.Apply(fact, false);
        var restoredMacd = new FuturesMacdSignalCommandState();
        foreach (var fact in macd.Events) restoredMacd.Apply(fact, false);
        Assert.True(restoredAdx.AdxSignals.Last().IsWarm);
        Assert.True(atr.Events.OfType<FuturesAtrSignalGeneratedEvent>().Last().FuturesAtrSignal.IsWarm);
        var lastBar = bars[^1];
        var nextBar = lastBar with
        {
            IntervalStartUtc = lastBar.IntervalEndUtc,
            IntervalEndUtc = lastBar.IntervalEndUtc.AddMinutes(5),
            FirstMarketEventUtc = lastBar.LastMarketEventUtc.AddMinutes(5),
            LastMarketEventUtc = lastBar.LastMarketEventUtc.AddMinutes(5),
            ObservationId = FuturesTradeSessionBarId.Create(lastBar.MarketSeriesIdentity,
                TimeFrameType.FiveMinutes, lastBar.IntervalEndUtc.AddMinutes(5), 1),
            Close = lastBar.Close + 1
        };
        var nextId = new FuturesAtrSignalId("ES-SEED", ValueDate, TimeFrameType.FiveMinutes,
            14, TimeOnly.FromDateTime(nextBar.LastMarketEventUtc.UtcDateTime));
        Assert.True(new GenerateFuturesAtrSignalCommand(nextId, nextBar.Close, nextBar).Execute(restoredAtr).Success);
        Assert.True(Assert.Single(restoredAtr.Events.OfType<FuturesAtrSignalGeneratedEvent>()).FuturesAtrSignal.IsWarm);
        var reinitialized = new FuturesAtrSignalCommandState();
        foreach (var fact in restoredAtr.Events) reinitialized.Apply(fact, false);
        Assert.True((atrCommand with { CommandId = Guid.NewGuid() }).Execute(reinitialized).Success);
        Assert.Equal(48, reinitialized.Events.OfType<FuturesAtrSignalGeneratedEvent>().Count());
        Assert.True(restoredMacd.MacdSignals.Last().IsWarm);
    }

    static FuturesTradeSessionBarReadModel[] Bars()
    {
        var calendar = new CmeFuturesMarketSessionCalendar();
        var series = MarketSeriesIdentity.ForContract("ES-SEED");
        return RsiHistoricalSeedWindowModel.Create(TimeFrameType.FiveMinutes, 48, Cutoff, calendar)
            .Select((window, index) => new FuturesTradeSessionBarReadModel
            {
                ContractId = "ES-SEED", MarketSeriesIdentity = series,
                ValueDate = window.ValueDate, TimeFrame = TimeFrameType.FiveMinutes,
                IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
                ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.FiveMinutes, window.EndUtc, 0),
                Open = 100 + index, High = 101 + index, Low = 99 + index, Close = 100 + index,
                FirstMarketEventUtc = window.EndUtc.AddTicks(-2),
                LastMarketEventUtc = window.EndUtc.AddTicks(-1),
                CalculatedAtUtc = Cutoff, IsComplete = true, IsValid = true,
                CalculationVersion = "four-hour-seed-test",
                CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
            }).ToArray();
    }
}
