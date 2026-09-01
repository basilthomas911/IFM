using FluentAssertions;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using CacheComponentType = TomasAI.IFM.Application.MarketData.MarketOutlook.MarketOutlookComponentType;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookCombinationVerificationTests
{
    static readonly MarketOutlookEntityId Id = new("ESZ26", new DateOnly(2026, 9, 1));

    [Fact]
    public void All127NonEmptyAvailabilityMasks_ProduceDeterministicPartialOrCompleteOutput()
    {
        for (var mask = 1; mask < 128; mask++)
        {
            var state = State(mask);
            var timestamp = new DateTime(2026, 9, 1, 15, 30, 0, DateTimeKind.Utc);

            var first = MarketOutlookComposer.Compose(
                state, MarketOutlookRefreshTrigger.Component, timestamp);
            var second = MarketOutlookComposer.Compose(
                state, MarketOutlookRefreshTrigger.Component, timestamp);

            first.Should().Be(second, $"mask {mask} must be deterministic");
            first.IsValid.Should().BeTrue($"non-empty mask {mask} has a usable input");
        }
    }

    [Fact]
    public void TenThousandAcceptedTrades_ReplacePreviewWithoutAdvancingCommittedDailyInput()
    {
        var cache = new MarketOutlookHotCache();
        var epoch = Guid.NewGuid();
        var baseline = SampleData.EodData with
        {
            Symbol = "ES",
            ContractId = Id.ContractId,
            ValueDate = Id.ValueDate,
            OpenPrice = 5_000m
        };
        cache.Write(Id,
            [new(CacheComponentType.Eod, new(Guid.NewGuid(), 1, DateTime.UtcNow.AddDays(-1)))],
            state => state with { FuturesEodData = baseline },
            state => MarketOutlookComposer.Compose(
                state, MarketOutlookRefreshTrigger.EodSession, DateTime.UtcNow));

        for (var ordinal = 1; ordinal <= 10_000; ordinal++)
        {
            cache.Write(Id,
                [new(CacheComponentType.EsTrade,
                    new(Guid.NewGuid(), 0, DateTime.UtcNow.AddTicks(ordinal), epoch, ordinal))],
                state => state with { CurrentEsPrice = 5_000m + ordinal / 100m },
                state => MarketOutlookComposer.Compose(
                    state, MarketOutlookRefreshTrigger.EsTrade, DateTime.UtcNow));
        }

        cache.TryGetInputs(Id, out var finalInputs).Should().BeTrue();
        finalInputs.FuturesEodData.Should().BeSameAs(baseline);
        finalInputs.Positions[CacheComponentType.EsTrade].StreamOrdinal.Should().Be(10_000);
        cache.GetMetrics().ComposedSnapshots.Should().Be(10_001);
    }

    static MarketOutlookInputState State(int mask)
    {
        var state = new MarketOutlookInputState { EntityId = Id };
        if ((mask & 1) != 0)
            state = state with
            {
                FuturesEodData = SampleData.EodData with
                {
                    Symbol = "ES",
                    ContractId = Id.ContractId,
                    ValueDate = Id.ValueDate
                }
            };
        if ((mask & 2) != 0)
            state = state with { FuturesRsiSignal = new FuturesRsiSignalReadModel() };
        if ((mask & 4) != 0)
            state = state with { FuturesTdiSignal = new FuturesTdiSignalReadModel() };
        if ((mask & 8) != 0)
            state = state with { LatestItiTrendSignal = new FuturesItiSignalV2ReadModel() };
        if ((mask & 16) != 0)
            state = state with { VixFuturesPrice = 20m };
        if ((mask & 32) != 0)
            state = state with { FuturesEmaSignal = new FuturesEmaSignalReadModel() };
        if ((mask & 64) != 0)
            state = state with { FuturesBbSignal = new FuturesBbSignalReadModel() };
        return state;
    }
}
