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
    public void PerComponentOrdering_RejectsDuplicateAndDelayedButAcceptsOrdinalGap()
    {
        var cache = new MarketOutlookHotCache();
        cache.Activate(new(Id.ContractId, Id.ValueDate, Guid.NewGuid()));
        var epoch = Guid.NewGuid();
        var t0 = DateTime.UtcNow;

        cache.TryUpdateInput(Id, CacheComponentType.EsTrade,
            new(Guid.NewGuid(), 0, t0, epoch, 1),
            state => state with { CurrentEsPrice = 100m }, out _).Should().BeTrue();
        cache.TryUpdateInput(Id, CacheComponentType.EsTrade,
            new(Guid.NewGuid(), 0, t0.AddSeconds(2), epoch, 3),
            state => state with { CurrentEsPrice = 103m }, out _).Should().BeTrue();
        cache.TryUpdateInput(Id, CacheComponentType.EsTrade,
            new(Guid.NewGuid(), 0, t0.AddSeconds(1), epoch, 2),
            state => state with { CurrentEsPrice = 102m }, out _).Should().BeFalse();

        cache.TryGetInputs(Id, out var state).Should().BeTrue();
        state.CurrentEsPrice.Should().Be(103m);
        cache.GetMetrics().RejectedInputUpdates.Should().Be(1);
    }

    [Fact]
    public void GenerationFence_RejectsOldValueDateAndEvictsPriorIdentity()
    {
        var cache = new MarketOutlookHotCache();
        cache.Activate(new(Id.ContractId, Id.ValueDate, Guid.NewGuid()));
        cache.TryUpdateInput(Id, CacheComponentType.Vx, Position(1),
            state => state with { VixFuturesPrice = 20m }, out _).Should().BeTrue();
        var next = new MarketOutlookEntityId("ESH27", Id.ValueDate.AddDays(1));

        cache.Activate(new(next.ContractId, next.ValueDate, Guid.NewGuid()));

        cache.TryGetInputs(Id, out _).Should().BeFalse();
        cache.TryUpdateInput(Id, CacheComponentType.Vx, Position(2),
            state => state with { VixFuturesPrice = 21m }, out _).Should().BeFalse();
        cache.TryUpdateInput(next, CacheComponentType.Vx, Position(3),
            state => state with { VixFuturesPrice = 22m }, out _).Should().BeTrue();
    }

    [Fact]
    public void NativeGenerationChange_ClearsSameContractAndValueDate()
    {
        var cache = new MarketOutlookHotCache();
        cache.Activate(new(Id.ContractId, Id.ValueDate, Guid.NewGuid()));
        cache.TryUpdateInput(Id, CacheComponentType.Vx, Position(1),
            state => state with { VixFuturesPrice = 20m }, out _).Should().BeTrue();

        cache.Activate(new(Id.ContractId, Id.ValueDate, Guid.NewGuid()));

        cache.TryGetInputs(Id, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentWriters_LeaveAtomicDeterministicHighestOrdinal()
    {
        var cache = new MarketOutlookHotCache();
        cache.Activate(new(Id.ContractId, Id.ValueDate, Guid.NewGuid()));
        var epoch = Guid.NewGuid();
        var t0 = DateTime.UtcNow;

        await Parallel.ForEachAsync(Enumerable.Range(1, 2_000), async (ordinal, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            cache.TryUpdateInput(Id, CacheComponentType.EsTrade,
                new(Guid.NewGuid(), 0, t0.AddTicks(ordinal), epoch, ordinal),
                state => state with { CurrentEsPrice = ordinal }, out _);
            await Task.Yield();
        });

        cache.TryGetInputs(Id, out var state).Should().BeTrue();
        state.CurrentEsPrice.Should().Be(2_000m);
        state.Positions[CacheComponentType.EsTrade].StreamOrdinal.Should().Be(2_000);
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

    static MarketOutlookSourcePosition Position(long sequence) =>
        new(Guid.NewGuid(), sequence, DateTime.UtcNow.AddTicks(sequence));
}
