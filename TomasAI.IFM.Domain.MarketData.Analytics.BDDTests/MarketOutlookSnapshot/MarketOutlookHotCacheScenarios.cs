using FluentAssertions;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using CacheComponentType = TomasAI.IFM.Application.MarketData.MarketOutlook.MarketOutlookComponentType;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.MarketOutlookSnapshot;

public sealed class MarketOutlookHotCacheScenarios
{
    static readonly MarketOutlookEntityId Id = new("ESZ26", new DateOnly(2026, 9, 1));

    public static TheoryData<CacheComponentType> IndependentComponents => new()
    {
        CacheComponentType.Rsi,
        CacheComponentType.Tdi,
        CacheComponentType.ItiLatest,
        CacheComponentType.Vx,
        CacheComponentType.Eod,
        CacheComponentType.Ema,
        CacheComponentType.BollingerBand,
        CacheComponentType.TradeSignal
    };

    [Theory]
    [MemberData(nameof(IndependentComponents))]
    public void GivenOneComponent_WhenItArrives_ThenItCanPublishWithoutWaitingForSiblings(
        CacheComponentType component)
    {
        var cache = new MarketOutlookHotCache();
        cache.Activate(new(Id.ContractId, Id.ValueDate, Guid.NewGuid()));
        var timestamp = DateTime.UtcNow;

        cache.TryUpdateInput(Id, component, new(Guid.NewGuid(), 1, timestamp),
            state => Add(component, state), out var inputs).Should().BeTrue();
        var projection = MarketOutlookComposer.Compose(
            inputs, MarketOutlookRefreshTrigger.Component, timestamp);
        cache.SetCurrent(projection);

        cache.TryGetCurrent(Id, out var current).Should().BeTrue();
        current.IsValid.Should().BeTrue();
        current.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.Component);
        current.MissingInputs.Should().NotBeNull();
    }

    [Theory]
    [InlineData(IntrinsicTimeModeType.Trending)]
    [InlineData(IntrinsicTimeModeType.TrendDirectionChanged)]
    [InlineData(IntrinsicTimeModeType.TrendExtremeChanged)]
    [InlineData(IntrinsicTimeModeType.TrendReversalChanged)]
    public void GivenAnyApprovedItiLanguage_WhenItArrives_ThenLatestTrendCanAdvance(
        IntrinsicTimeModeType mode)
    {
        var signal = new FuturesItiSignalV2ReadModel
        {
            ContractId = Id.ContractId,
            ValueDate = Id.ValueDate,
            TimePeriod = TimeFrameType.Daily,
            IntrinsicTimeMode = mode,
            TrendDelta = 12.5
        };
        var state = new MarketOutlookInputState { EntityId = Id, LatestItiTrendSignal = signal };

        var projection = MarketOutlookComposer.Compose(
            state, MarketOutlookRefreshTrigger.Component, DateTime.UtcNow);

        projection.LatestItiTrendSignal.Should().Be(signal);
    }

    [Fact]
    public void GivenAStoppedEsSource_WhenAnotherComponentRefreshes_ThenValuesRemainVisibleButHealthIsRed()
    {
        var oldTrade = DateTime.UtcNow.AddMinutes(-16);
        var state = Add(CacheComponentType.Eod, new MarketOutlookInputState
        {
            EntityId = Id,
            CurrentEsPrice = 5_100m,
            Positions = System.Collections.Immutable.ImmutableDictionary<CacheComponentType, MarketOutlookSourcePosition>
                .Empty.Add(CacheComponentType.EsTrade, new(Guid.NewGuid(), 1, oldTrade))
        });

        var projection = MarketOutlookComposer.Compose(
            state, MarketOutlookRefreshTrigger.Component, DateTime.UtcNow);

        projection.FuturesEodData.ClosePrice.Should().Be(5_100m);
        projection.FeedHealth.Should().Be("Red");
        projection.EsPriceAvailability.Should().Be(MarketOutlookInputAvailability.Stale);
    }

    static MarketOutlookInputState Add(CacheComponentType component, MarketOutlookInputState state) => component switch
    {
        CacheComponentType.Rsi => state with { FuturesRsiSignal = new FuturesRsiSignalReadModel() },
        CacheComponentType.Tdi => state with { FuturesTdiSignal = new FuturesTdiSignalReadModel() },
        CacheComponentType.ItiLatest => state with { LatestItiTrendSignal = new FuturesItiSignalV2ReadModel() },
        CacheComponentType.Vx => state with { VixFuturesPrice = 20m },
        CacheComponentType.Eod => state with
        {
            FuturesEodData = new FuturesEodDataV2ReadModel
            {
                Symbol = "ES",
                ContractId = Id.ContractId,
                ValueDate = Id.ValueDate,
                OpenPrice = 5_000m,
                HighPrice = 5_100m,
                LowPrice = 4_900m,
                ClosePrice = 5_050m
            }
        },
        CacheComponentType.Ema => state with { FuturesEmaSignal = new FuturesEmaSignalReadModel() },
        CacheComponentType.BollingerBand => state with { FuturesBbSignal = new FuturesBbSignalReadModel() },
        CacheComponentType.TradeSignal => state with { FuturesTradeSignal = new FuturesTradeSignalV2ReadModel() },
        _ => state
    };
}
