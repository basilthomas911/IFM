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
    public void RegistryIsolatesDatesAndSeriesAndKeepsContinuationContractsTogether()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        var registry = CreateRegistry(clock);
        var date = new DateOnly(2026, 8, 25);
        var first = registry.Get(registry.Resolve("ESU6", date));
        var sameSeries = registry.Get(registry.Resolve("ESZ6", date));
        var otherSeries = registry.Get(registry.Resolve("VXU6", date));
        var next = registry.Get(registry.Resolve("ESU6", date.AddDays(1)));

        Assert.Same(first, sameSeries);
        Assert.NotSame(first, otherSeries);
        Assert.NotSame(first, next);
        Assert.Equal(3, registry.Count);
    }

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
        var closed = state.CloseThrough(session.EndUtc.AddSeconds(2));

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
    public void MarketEventClockAheadOfHost_DoesNotInvalidateCompletedBar()
    {
        var valueDate = new DateOnly(2026, 8, 25);
        var start = Calendar.GetSession(valueDate).StartUtc;
        var marketTime = start.AddSeconds(14).AddMilliseconds(961);
        var clockTime = start.AddSeconds(14).AddMilliseconds(621);
        var state = CreateAccumulator(new FixedTimeProvider(clockTime));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, marketTime, Guid.NewGuid(), 1, 100)));

        var bar = Assert.Single(state.CloseThrough(start.AddSeconds(17)),
            value => value.TimeFrame == TimeFrameType.FifteenSeconds);
        Assert.True(bar.CalculatedAtUtc < bar.LastMarketEventUtc);
        Assert.Empty(new FuturesTradeSessionBarReadModelValidationRules().Execute(bar));
    }

    [Fact]
    public void IncreasingTradeOrdinalWithRegressingEventTimestamp_PreservesEventTimeRange()
    {
        var valueDate = new DateOnly(2026, 8, 25);
        var start = Calendar.GetSession(valueDate).StartUtc;
        var epoch = Guid.NewGuid();
        var state = CreateAccumulator(new FixedTimeProvider(start.AddMinutes(1)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, start.AddSeconds(2), epoch, 1, 100)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, start.AddSeconds(1), epoch, 2, 101)));

        var bar = Assert.Single(state.CloseThrough(start.AddSeconds(17)),
            value => value.TimeFrame == TimeFrameType.FifteenSeconds);
        Assert.Equal(start.AddSeconds(1), bar.FirstMarketEventUtc);
        Assert.Equal(start.AddSeconds(2), bar.LastMarketEventUtc);
        Assert.Equal(100m, bar.Open);
        Assert.Equal(101m, bar.Close);
        Assert.Empty(new FuturesTradeSessionBarReadModelValidationRules().Execute(bar));
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
        var recovered = Assert.Single(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(2), epoch, 2, 99)));
        Assert.Equal(2, recovered.TradeCount);
        Assert.Equal(99m, recovered.Close);
        Assert.Empty(state.Accept(Trade("ESZ6", valueDate, session.StartUtc.AddSeconds(20), Guid.NewGuid(), 1, 102)));

        var closed = state.CloseThrough(session.EndUtc.AddSeconds(2));
        Assert.Equal(7, closed.Count);
        Assert.All(closed, x => Assert.Equal("ESZ6", x.ContractId));
    }

    [Fact]
    public void ExpiredGapSuppressesAffectedIntervalsAndResumesWithinTheSameEpoch()
    {
        var valueDate = new DateOnly(2026, 8, 25);
        var session = Calendar.GetSession(valueDate);
        var epoch = Guid.NewGuid();
        var clock = new FixedTimeProvider(session.StartUtc.AddSeconds(17));
        var state = CreateAccumulator(clock);

        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(1), epoch, 1, 100)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(17), epoch, 3, 101)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(32), epoch, 4, 102)));
        Assert.Empty(state.CloseThrough(session.StartUtc.AddSeconds(18)));
        Assert.Empty(state.CloseThrough(session.StartUtc.AddSeconds(19)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(2), epoch, 2, 99)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, session.StartUtc.AddSeconds(33), epoch, 4, 999)));

        var recovered = Assert.Single(state.CloseThrough(session.StartUtc.AddSeconds(47)));
        Assert.Equal(TimeFrameType.FifteenSeconds, recovered.TimeFrame);
        Assert.Equal(1, recovered.TradeCount);
        Assert.Equal(102m, recovered.Close);
        Assert.Equal(epoch, recovered.StreamEpochId);
    }

    [Fact]
    public void FiveAndFifteenMinuteBarsResumeAfterAnUnfilledGap()
    {
        var valueDate = new DateOnly(2026, 8, 25);
        var start = Calendar.GetSession(valueDate).StartUtc;
        var epoch = Guid.NewGuid();
        var state = CreateAccumulator(new FixedTimeProvider(start.AddMinutes(3)));

        state.Accept(Trade("ESU6", valueDate, start.AddMinutes(1), epoch, 1, 100));
        state.Accept(Trade("ESU6", valueDate, start.AddMinutes(3), epoch, 3, 103));
        Assert.Empty(state.CloseThrough(start.AddMinutes(3).AddSeconds(2)));

        Assert.Empty(state.Accept(Trade("ESU6", valueDate, start.AddMinutes(16), epoch, 4, 104)));
        Assert.Empty(state.Accept(Trade("ESU6", valueDate, start.AddMinutes(17), epoch, 4, 999)));
        var bars = state.CloseThrough(start.AddMinutes(30).AddSeconds(2));

        foreach (var timeFrame in new[] { TimeFrameType.FiveMinutes, TimeFrameType.FifteenMinutes })
        {
            var bar = Assert.Single(bars, value => value.TimeFrame == timeFrame);
            Assert.Equal(start.AddMinutes(15), bar.IntervalStartUtc);
            Assert.Equal(1, bar.TradeCount);
            Assert.Equal(104m, bar.Close);
            Assert.True(bar.IsComplete && bar.IsValid);
            Assert.Equal(epoch, bar.StreamEpochId);
        }
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

    static FuturesTradeSessionBarAccumulatorRegistry CreateRegistry(TimeProvider timeProvider) => new(
        Calendar,
        new PrefixFuturesTradeSessionBarSeriesResolver(
            new Dictionary<string, MarketSeriesIdentity>(StringComparer.OrdinalIgnoreCase)
            {
                ["ES"] = Series
            }),
        timeProvider);
}
