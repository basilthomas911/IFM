using FluentAssertions;
using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using CacheComponentType = TomasAI.IFM.Application.MarketData.MarketOutlook.MarketOutlookComponentType;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookHotCacheTests
{
    static readonly MarketOutlookEntityId Id = new("ESZ26", new DateOnly(2026, 9, 1));

    [Fact]
    public void VersionlessContract_RoundTripsWithoutRevisionOrLifecycleState()
    {
        typeof(MarketOutlookReadModel).GetProperty("Revision").Should().BeNull();
        typeof(MarketOutlookReadModel).GetProperty("Status").Should().BeNull();
        var expected = new MarketOutlookReadModel
        {
            ContractId = Id.ContractId,
            ValueDate = Id.ValueDate,
            UpdatedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = DateTime.UtcNow,
            RefreshTrigger = MarketOutlookRefreshTrigger.Component,
            VixFuturesPrice = 21.5m,
            VxAvailability = MarketOutlookInputAvailability.Available
        };

        var actual = MessagePackSerializer.Deserialize<MarketOutlookReadModel>(
            MessagePackSerializer.Serialize(expected));

        actual.Should().Be(expected);
    }

    [Fact]
    public void LatestArrivalAlwaysOverwritesWithoutSourceOrderingRejection()
    {
        var cache = new MarketOutlookHotCache();
        var epoch = Guid.NewGuid();
        var t0 = DateTime.UtcNow;

        Write(cache, Id, CacheComponentType.EsTrade,
            new(Guid.NewGuid(), 0, t0.AddSeconds(2), epoch, 3),
            state => state with { CurrentEsPrice = 103m });
        Write(cache, Id, CacheComponentType.EsTrade,
            new(Guid.NewGuid(), 0, t0.AddSeconds(1), epoch, 2),
            state => state with { CurrentEsPrice = 102m });

        cache.TryGetInputs(Id, out var state).Should().BeTrue();
        state.CurrentEsPrice.Should().Be(102m, "the last routed arrival owns the slot");
        state.Positions[CacheComponentType.EsTrade].StreamOrdinal.Should().Be(2);
        typeof(MarketOutlookHotCacheMetrics).GetProperty("RejectedInputUpdates").Should().BeNull();
        cache.GetMetrics().ReceivedInputUpdates.Should().Be(2);
    }

    [Fact]
    public void SeparateContractAndValueDateIdentitiesNeverEvictOrMixEachOther()
    {
        var cache = new MarketOutlookHotCache();
        var next = new MarketOutlookEntityId("ESH27", Id.ValueDate.AddDays(1));

        Write(cache, Id, CacheComponentType.Vx, Position(1),
            state => state with { VixFuturesPrice = 20m });
        Write(cache, next, CacheComponentType.Vx, Position(2),
            state => state with { VixFuturesPrice = 22m });

        cache.TryGetCurrent(Id, out var first).Should().BeTrue();
        cache.TryGetCurrent(next, out var second).Should().BeTrue();
        first.VixFuturesPrice.Should().Be(20m);
        second.VixFuturesPrice.Should().Be(22m);
    }

    [Fact]
    public async Task ConcurrentPartialWritersPreserveSiblingFieldsAndPublishAtomicSnapshots()
    {
        var cache = new MarketOutlookHotCache();
        var t0 = DateTime.UtcNow;
        var readers = Enumerable.Range(0, 4).Select(async _ =>
        {
            for (var index = 0; index < 2_000; index++)
            {
                if (cache.TryGetCurrent(Id, out var snapshot))
                {
                    snapshot.ContractId.Should().Be(Id.ContractId);
                    snapshot.ValueDate.Should().Be(Id.ValueDate);
                }
                await Task.Yield();
            }
        });
        var writers = new[]
        {
            Task.Run(() =>
            {
                for (var index = 1; index <= 1_000; index++)
                    Write(cache, Id, CacheComponentType.EsTrade,
                        new(Guid.NewGuid(), index, t0.AddTicks(index)),
                        state => state with { CurrentEsPrice = index });
            }),
            Task.Run(() =>
            {
                for (var index = 1; index <= 1_000; index++)
                    Write(cache, Id, CacheComponentType.Vx,
                        new(Guid.NewGuid(), index, t0.AddTicks(index)),
                        state => state with { VixFuturesPrice = index });
            })
        };

        await Task.WhenAll(readers.Concat(writers));
        Write(cache, Id, CacheComponentType.EsTrade, Position(2_001),
            state => state with { CurrentEsPrice = 2_001m });

        cache.TryGetInputs(Id, out var state).Should().BeTrue();
        state.CurrentEsPrice.Should().Be(2_001m);
        state.VixFuturesPrice.Should().NotBeNull();
        cache.GetMetrics().ComposedSnapshots.Should().Be(2_001);
    }

    [Fact]
    public void PartialComposer_ReturnsUsefulOutputWithoutThrowing()
    {
        var state = new MarketOutlookInputState
        {
            EntityId = Id,
            CurrentEsPrice = 5_100m,
            MarketDataAsOfUtc = DateTime.UtcNow
        };

        var result = MarketOutlookComposer.Compose(
            state, MarketOutlookRefreshTrigger.EsTrade, DateTime.UtcNow);

        result.IsValid.Should().BeTrue();
        result.FuturesEodData.ClosePrice.Should().Be(5_100m);
        result.MissingInputs.Should().Contain("RSI").And.Contain("EMA");
        result.DailyAnalyticsAvailability.Should().Be(MarketOutlookInputAvailability.Unavailable);
    }

    [Fact]
    public void LivePriceComposer_UsesOpenToCurrentRatioForPercentFormattedUi()
    {
        var eod = SampleData.EodData with
        {
            Symbol = "ES",
            ContractId = Id.ContractId,
            ValueDate = Id.ValueDate,
            OpenPrice = 5_000m,
            ClosePrice = 5_000m
        };
        var result = MarketOutlookComposer.Compose(new MarketOutlookInputState
        {
            EntityId = Id,
            FuturesEodData = eod,
            CurrentEsPrice = 5_050m,
            MarketDataAsOfUtc = DateTime.UtcNow
        }, MarketOutlookRefreshTrigger.EsTrade, DateTime.UtcNow);

        result.FuturesEodData.DailyPercentChange.Should().BeApproximately(0.01d, 0.0000001d);
        result.FuturesEodData.ClosePrice.Should().Be(5_050m);
    }

    [Theory]
    [InlineData(5, "Green", MarketOutlookInputAvailability.Available)]
    [InlineData(6, "Yellow", MarketOutlookInputAvailability.Available)]
    [InlineData(15, "Yellow", MarketOutlookInputAvailability.Available)]
    [InlineData(16, "Red", MarketOutlookInputAvailability.Stale)]
    public void EsInputFreshness_UsesFiveAndFifteenMinuteBoundaries(
        int ageMinutes,
        string expectedHealth,
        MarketOutlookInputAvailability expectedAvailability)
    {
        var now = new DateTime(2026, 9, 1, 16, 0, 0, DateTimeKind.Utc);
        var sourceTime = now.AddMinutes(-ageMinutes);
        var state = new MarketOutlookInputState
        {
            EntityId = Id,
            CurrentEsPrice = 5_100m,
            MarketDataAsOfUtc = sourceTime,
            Positions = ImmutableDictionary<CacheComponentType, MarketOutlookSourcePosition>.Empty
                .Add(CacheComponentType.EsTrade, new(Guid.NewGuid(), 1, sourceTime))
        };

        var result = MarketOutlookComposer.Compose(
            state, MarketOutlookRefreshTrigger.Component, now);

        result.FeedHealth.Should().Be(expectedHealth);
        result.EsPriceAvailability.Should().Be(expectedAvailability);
        result.MarketDataAsOfUtc.Should().Be(sourceTime);
    }

    [Fact]
    public void RecompositionTime_DoesNotFabricateMarketDataTimestamp()
    {
        var sourceTime = new DateTime(2026, 9, 1, 15, 0, 0, DateTimeKind.Utc);
        var state = new MarketOutlookInputState
        {
            EntityId = Id,
            CurrentEsPrice = 5_100m,
            MarketDataAsOfUtc = sourceTime
        };

        var result = MarketOutlookComposer.Compose(
            state, MarketOutlookRefreshTrigger.Component, sourceTime.AddHours(1));

        result.UpdatedAtUtc.Should().BeAfter(result.MarketDataAsOfUtc);
        result.MarketDataAsOfUtc.Should().Be(sourceTime);
    }

    [Theory]
    [InlineData("Up", "Databento connection and required subscriptions are active")]
    [InlineData("Resetting", "Native reset attempt 2 of 3")]
    [InlineData("Down", "Required ES subscription could not be restored")]
    public void ExplicitFeedHealth_IsIndependentFromAnalyticsFreshness(
        string health,
        string reason)
    {
        var oldReceipt = DateTime.UtcNow.AddHours(-1);
        var state = new MarketOutlookInputState
        {
            EntityId = Id,
            CurrentEsPrice = 5_100m,
            FeedHealth = health,
            FeedHealthReason = reason,
            Positions = ImmutableDictionary<CacheComponentType, MarketOutlookSourcePosition>.Empty
                .Add(CacheComponentType.EsTrade, new(Guid.NewGuid(), 1, oldReceipt))
        };

        var result = MarketOutlookComposer.Compose(
            state, MarketOutlookRefreshTrigger.Component, DateTime.UtcNow);

        result.FeedHealth.Should().Be(health);
        result.FeedHealthReason.Should().Be(reason);
        result.EsPriceAvailability.Should().Be(MarketOutlookInputAvailability.Stale);
    }

    [Fact]
    public void FeedHealthWrite_CannotClearAnalyticsValues()
    {
        var cache = new MarketOutlookHotCache();
        Write(cache, Id, CacheComponentType.EsTrade, Position(1),
            state => state with { CurrentEsPrice = 5_100m });

        Write(cache, Id, CacheComponentType.FeedHealth, Position(2),
            state => state with
            {
                FeedHealth = "Resetting",
                FeedHealthReason = "Watchdog reset is in progress"
            });

        cache.TryGetCurrent(Id, out var snapshot).Should().BeTrue();
        snapshot.FuturesEodData.ClosePrice.Should().Be(5_100m);
        snapshot.FeedHealth.Should().Be("Resetting");
    }

    [Fact]
    public void ComposerFailure_ReleasesWriteLockAndRetainsLastCompleteSnapshot()
    {
        var cache = new MarketOutlookHotCache();
        Write(cache, Id, CacheComponentType.EsTrade, Position(1),
            state => state with { CurrentEsPrice = 5_100m });

        Action failedWrite = () => cache.Write(
            Id,
            [new(CacheComponentType.Vx, Position(2))],
            state => state with { VixFuturesPrice = 21m },
            _ => throw new InvalidOperationException("injected composer failure"));

        failedWrite.Should().Throw<InvalidOperationException>();
        Write(cache, Id, CacheComponentType.Vx, Position(3),
            state => state with { VixFuturesPrice = 22m });

        cache.TryGetCurrent(Id, out var snapshot).Should().BeTrue();
        snapshot.FuturesEodData.ClosePrice.Should().Be(5_100m);
        snapshot.VixFuturesPrice.Should().Be(22m);
        cache.GetMetrics().CompositionFailures.Should().Be(1);
        cache.GetMetrics().WrittenInputUpdates.Should().Be(2);
    }

    static MarketOutlookHotCacheWriteResult Write(
        MarketOutlookHotCache cache,
        MarketOutlookEntityId id,
        CacheComponentType component,
        MarketOutlookSourcePosition position,
        Func<MarketOutlookInputState, MarketOutlookInputState> update) =>
        cache.Write(id, [new(component, position)], update,
            state => MarketOutlookComposer.Compose(
                state, MarketOutlookRefreshTrigger.Component, DateTime.UtcNow));

    static MarketOutlookSourcePosition Position(long sequence) =>
        new(Guid.NewGuid(), sequence, DateTime.UtcNow.AddTicks(sequence));
}
