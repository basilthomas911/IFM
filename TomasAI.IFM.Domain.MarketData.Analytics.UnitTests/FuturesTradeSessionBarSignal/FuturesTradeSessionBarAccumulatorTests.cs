using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSessionBarSignal;

public sealed class FuturesTradeSessionBarAccumulatorTests
{
    static readonly IMarketSessionCalendar Calendar = new CmeFuturesMarketSessionCalendar();
    static readonly MarketSeriesIdentity Series = MarketSeriesIdentity.ForFuturesSeries(
        new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));

    [Fact]
    public void SessionBarrierClosesSixIntradaySchedulesAndDailyExactlyOnce()
    {
        var valueDate = new DateOnly(2026, 8, 25);
        var session = Calendar.GetSession(valueDate);
        var epoch = Guid.NewGuid();
        var clock = new FixedTimeProvider(session.EndUtc.AddMinutes(1));
        var state = CreateAccumulator(clock);

        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(1), epoch, 1, 100)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(1), epoch, 1, 100)));
        var closed = state.CloseThrough(session.EndUtc);

        Assert.Equal(7, closed.Count);
        Assert.Equal(7, closed.Select(x => x.TimeFrame).Distinct().Count());
        Assert.Equal(7, closed.Select(x => x.ObservationId).Distinct().Count());
        Assert.All(closed, x =>
        {
            Assert.Equal(1, x.TradeCount);
            Assert.Equal(100m, x.Open);
            Assert.Equal(epoch, x.StreamEpochId);
            Assert.Equal((ushort)2, x.SchemaVersion);
            Assert.Equal(x.ObservationId,
                FuturesTradeSessionBarId.Create(
                    x.MarketSeriesIdentity, x.TimeFrame, x.IntervalEndUtc, x.LastSourceSequence));
        });
        Assert.Empty(state.CloseThrough(session.EndUtc));
    }

    [Fact]
    public void SourceGapOutOfOrderAndRollNeverPublishAValidLookingPartialInterval()
    {
        var valueDate = new DateOnly(2026, 8, 25);
        var session = Calendar.GetSession(valueDate);
        var epoch = Guid.NewGuid();
        var clock = new FixedTimeProvider(session.EndUtc.AddMinutes(1));
        var state = CreateAccumulator(clock);

        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(1), epoch, 1, 100)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(17), epoch, 3, 101)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(2), epoch, 2, 99)));
        Assert.Empty(state.Accept(Trade("ESZ6", valueDate, session.StartUtc.AddSeconds(20), Guid.NewGuid(), 1, 102)));

        var closed = state.CloseThrough(session.EndUtc);
        Assert.Equal(7, closed.Count);
        Assert.All(closed, x => Assert.Equal("ESZ6", x.ContractId));
    }

    [Fact]
    public void SourceGapInvalidatesTheEpochUntilAnExplicitEpochRecoveryBarrier()
    {
        var valueDate = new DateOnly(2026, 8, 25);
        var session = Calendar.GetSession(valueDate);
        var invalidEpoch = Guid.NewGuid();
        var clock = new FixedTimeProvider(session.EndUtc.AddMinutes(1));
        var state = CreateAccumulator(clock);

        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(1), invalidEpoch, 1, 100)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(17), invalidEpoch, 3, 101)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(32), invalidEpoch, 4, 102)));
        Assert.Empty(state.CloseThrough(session.EndUtc));

        var recoveredEpoch = Guid.NewGuid();
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(47), recoveredEpoch, 1, 103)));
        var recovered = state.CloseThrough(session.EndUtc);
        Assert.Equal(7, recovered.Count);
        Assert.All(recovered, value =>
        {
            Assert.Equal(103m, value.Close);
            Assert.Equal(recoveredEpoch, value.StreamEpochId);
        });
    }

    static FuturesMarketPriceUpdatedRealtimeEvent Trade(
        string contractId,
        DateOnly valueDate,
        DateTimeOffset timestamp,
        Guid epoch,
        long ordinal,
        decimal price) => new()
    {
        Subject = new ActorSubject(ActorType.Realtime,
            FuturesMarketPriceUpdatedRealtimeEvent.Actor,
            FuturesMarketPriceUpdatedRealtimeEvent.Verb,
            $"{contractId}-{valueDate:O}"),
        Id = Guid.NewGuid(),
        EntityId = new(contractId, valueDate, AssetTypeId.Futures),
        AggregateId = contractId,
        EventSource = "test",
        ReceivedOn = timestamp.UtcDateTime,
        UpdateSource = FuturesMarketPriceUpdateSource.Trade,
        Price = new FuturesMarketPriceSnapshot(
            contractId, 1, 1, AssetTypeId.Futures, valueDate, null,
            new FuturesMarketTradeSnapshot(
                price, 1, ordinal, timestamp, timestamp,
                NormalizedTradeAction.New, NormalizedTradeSide.Buy,
                NormalizedTradeConditionFlags.None, epoch, ordinal))
    };

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static FuturesTradeSessionBarAccumulator CreateAccumulator(TimeProvider timeProvider) => new(
        Calendar,
        new PrefixFuturesTradeSessionBarSeriesResolver(
            new Dictionary<string, MarketSeriesIdentity>(StringComparer.OrdinalIgnoreCase)
            {
                ["ES"] = Series
            }),
        timeProvider);
}
