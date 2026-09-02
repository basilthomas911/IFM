using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
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
    public async Task GivenOneComponent_WhenItArrives_ThenItCanPublishWithoutWaitingForSiblings(
        CacheComponentType component)
    {
        var cache = new MarketOutlookHotCache();
        var metrics = new MarketOutlookProcessorMetrics();
        var channel = new MarketOutlookUpdateChannel(metrics);
        var publisher = Substitute.For<IMarketOutlookSnapshotPublisher>();
        using var processor = new MarketOutlookUpdateProcessor(
            channel, channel, cache, cache, publisher, metrics,
            Substitute.For<ILogger<MarketOutlookUpdateProcessor>>());
        var timestamp = DateTime.UtcNow;
        await processor.StartAsync(CancellationToken.None);
        try
        {
            channel.Submit(Update(component, timestamp));
            (await processor.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

            cache.TryGetCurrent(Id, out var current).Should().BeTrue();
            current.IsValid.Should().BeTrue();
            current.RefreshTrigger.Should().Be(component == CacheComponentType.Eod
                ? MarketOutlookRefreshTrigger.EodSession
                : MarketOutlookRefreshTrigger.Component);
            current.MissingInputs.Should().NotBeNull();
            await publisher.Received(1).PublishAsync(
                Arg.Any<MarketOutlookUpdate>(),
                Arg.Any<MarketOutlookReadModel>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await processor.StopAsync(CancellationToken.None);
        }
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

    [Theory]
    [InlineData(18, 19, PriceVolatilityType.Rising)]
    [InlineData(18, 17, PriceVolatilityType.Falling)]
    [InlineData(18, 18, PriceVolatilityType.Flat)]
    public void GivenVxSessionOpenAndCurrentPrice_WhenMarketOutlookComposes_ThenPriceVolatilityIsClassified(
        decimal sessionOpen,
        decimal current,
        PriceVolatilityType expected)
    {
        var state = new MarketOutlookInputState
        {
            EntityId = Id,
            VixFuturesSessionOpenPrice = sessionOpen,
            VixFuturesPrice = current
        };

        var projection = MarketOutlookComposer.Compose(
            state,
            MarketOutlookRefreshTrigger.Component,
            DateTime.UtcNow);

        projection.FuturesEodData.PriceVolatility.Should().Be(expected);
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

    static MarketOutlookUpdate Update(CacheComponentType component, DateTime timestamp) => component switch
    {
        CacheComponentType.Rsi => new RsiMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(), EntityId = Id, ReceivedAtUtc = timestamp,
            MarketDataAsOfUtc = timestamp, Signal = new()
        },
        CacheComponentType.Tdi => new TdiMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(), EntityId = Id, ReceivedAtUtc = timestamp,
            MarketDataAsOfUtc = timestamp, Signal = new()
        },
        CacheComponentType.ItiLatest => new ItiMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(), EntityId = Id, ReceivedAtUtc = timestamp,
            MarketDataAsOfUtc = timestamp, Signal = new()
        },
        CacheComponentType.Vx => new VixPriceMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(), EntityId = Id, ReceivedAtUtc = timestamp,
            MarketDataAsOfUtc = timestamp, Price = 20m
        },
        CacheComponentType.Eod => new EodMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(), EntityId = Id, ReceivedAtUtc = timestamp,
            MarketDataAsOfUtc = timestamp,
            Eod = new()
            {
                Symbol = "ES", ContractId = Id.ContractId, ValueDate = Id.ValueDate,
                OpenPrice = 5_000m, HighPrice = 5_100m, LowPrice = 4_900m, ClosePrice = 5_050m
            }
        },
        CacheComponentType.Ema => new EmaMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(), EntityId = Id, ReceivedAtUtc = timestamp,
            MarketDataAsOfUtc = timestamp, Signal = new()
        },
        CacheComponentType.BollingerBand => new BollingerBandMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(), EntityId = Id, ReceivedAtUtc = timestamp,
            MarketDataAsOfUtc = timestamp, Signal = new()
        },
        CacheComponentType.TradeSignal => new TradeSignalMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(), EntityId = Id, ReceivedAtUtc = timestamp,
            MarketDataAsOfUtc = timestamp, Signal = new()
        },
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, null)
    };
}
