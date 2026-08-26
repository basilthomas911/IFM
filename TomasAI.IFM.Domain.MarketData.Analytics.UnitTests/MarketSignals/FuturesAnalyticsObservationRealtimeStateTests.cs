using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketSignals;

public sealed class FuturesAnalyticsObservationRealtimeStateTests
{
    static readonly IMarketSessionCalendar Calendar = new CmeFuturesMarketSessionCalendar();
    static readonly MarketSeriesIdentity Series = MarketSeriesIdentity.ForFuturesSeries(
        new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));

    [Fact]
    public void SessionBarrierClosesSixIntradaySchedulesAndDailyExactlyOnce()
    {
        var state = new FuturesAnalyticsObservationRealtimeState();
        var valueDate = new DateOnly(2026, 8, 25);
        var session = Calendar.GetSession(valueDate);
        var epoch = Guid.NewGuid();
        var clock = new FixedTimeProvider(session.EndUtc.AddMinutes(1));

        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(1), epoch, 1, 100), Series, Calendar, clock));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(1), epoch, 1, 100), Series, Calendar, clock));
        var closed = state.CloseThrough(session.EndUtc, clock);

        Assert.Equal(7, closed.Count);
        Assert.Equal(7, closed.Select(x => x.TimeFrame).Distinct().Count());
        Assert.Equal(7, closed.Select(x => x.ObservationId).Distinct().Count());
        Assert.All(closed, x =>
        {
            Assert.Equal(1, x.TradeCount);
            Assert.Equal(100m, x.Open);
            Assert.Equal(x.ObservationId,
                FuturesAnalyticsObservationId.Create(
                    x.MarketSeriesIdentity, x.TimeFrame, x.IntervalEndUtc, x.LastSourceSequence));
        });
        Assert.Empty(state.CloseThrough(session.EndUtc, clock));
    }

    [Fact]
    public void SourceGapOutOfOrderAndRollNeverPublishAValidLookingPartialInterval()
    {
        var state = new FuturesAnalyticsObservationRealtimeState();
        var valueDate = new DateOnly(2026, 8, 25);
        var session = Calendar.GetSession(valueDate);
        var epoch = Guid.NewGuid();
        var clock = new FixedTimeProvider(session.EndUtc.AddMinutes(1));

        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(1), epoch, 1, 100), Series, Calendar, clock));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(17), epoch, 3, 101), Series, Calendar, clock));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(2), epoch, 2, 99), Series, Calendar, clock));
        Assert.Empty(state.Accept(Trade("ESZ6", valueDate, session.StartUtc.AddSeconds(20), Guid.NewGuid(), 1, 102), Series, Calendar, clock));

        var closed = state.CloseThrough(session.EndUtc, clock);
        Assert.Equal(7, closed.Count);
        Assert.All(closed, x => Assert.Equal("ESZ6", x.ContractId));
    }

    [Fact]
    public void SourceGapInvalidatesTheEpochUntilAnExplicitEpochRecoveryBarrier()
    {
        var state = new FuturesAnalyticsObservationRealtimeState();
        var valueDate = new DateOnly(2026, 8, 25);
        var session = Calendar.GetSession(valueDate);
        var invalidEpoch = Guid.NewGuid();
        var clock = new FixedTimeProvider(session.EndUtc.AddMinutes(1));

        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(1), invalidEpoch, 1, 100), Series, Calendar, clock));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(17), invalidEpoch, 3, 101), Series, Calendar, clock));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(32), invalidEpoch, 4, 102), Series, Calendar, clock));
        Assert.Empty(state.CloseThrough(session.EndUtc, clock));

        var recoveredEpoch = Guid.NewGuid();
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(47), recoveredEpoch, 1, 103), Series, Calendar, clock));
        var recovered = state.CloseThrough(session.EndUtc, clock);
        Assert.Equal(7, recovered.Count);
        Assert.All(recovered, value => Assert.Equal(103m, value.Close));
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
}
