using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

/// <summary>Qualifies local raw-trade history without substituting close-only or missing data.</summary>
public sealed class StoredSessionIndicatorSeedTests
{
    [Fact]
    public async Task Daily_oscillators_reuse_complete_unadjusted_baseline_sessions_without_provider_acquisition()
    {
        var db = Substitute.For<IDbContextFactory>();
        var market = Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();
        var series = TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common.MarketSeriesIdentity.ForFuturesSeries(new("ES", "calendar-front", "unadjusted", 1));
        var calendar = new CmeFuturesMarketSessionCalendar();
        var now = Start.AddHours(3);
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.Daily, 48, now, calendar);
        var original = windows.Select((window, index) => new TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesEodObservationReadModel
        {
            MarketSeriesIdentity = series, ContractId = "ES.c.0", ValueDate = window.ValueDate,
            SessionStartUtc = window.StartUtc, SessionEndUtc = window.EndUtc,
            ObservationId = TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarId.Create(series, TimeFrameType.Daily, window.EndUtc, index + 1),
            Open = 100, High = 102, Low = 99, Close = 101, Volume = 100,
            FirstSourceSequence = index + 1, LastSourceSequence = index + 1,
            FirstMarketEventUtc = window.StartUtc, LastMarketEventUtc = window.EndUtc.AddSeconds(-1), IsComplete = true, IsValid = true
        }).ToArray();
        ((IHistoricalObservationStore)db.MarketDataDb).GetRawEodRangeAsync(series, windows[0].ValueDate, windows[^1].ValueDate, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyList<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesEodObservationReadModel>>(original));
        var reader = new StoredSessionIndicatorSeedReader(db, calendar, new Clock(now), NullLogger<StoredSessionIndicatorSeedReader>.Instance, market);
        var bars = await reader.ReadAsync("ES20261218", TimeFrameType.Daily, 48, default);
        Assert.Equal(48, bars.Length);
        Assert.All(bars, bar => { Assert.Equal("ES20261218", bar.ContractId); Assert.Equal(series, bar.MarketSeriesIdentity); });
        Assert.Equal(original.Select(value => value.ObservationId), bars.Select(value => value.ObservationId));
        Assert.All(original, value => Assert.Equal("ES.c.0", value.ContractId));
        await market.DidNotReceive().ReadIndicatorSeedBarsAsync(Arg.Any<string>(), Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_persisted_complete_interval_avoids_raw_scans_and_provider_acquisition()
    {
        var db = Substitute.For<IDbContextFactory>();
        var ticks = Substitute.For<ITradeTickQuoteDbContext>();
        db.TradeTickQuoteDb.Returns(ticks);
        var saved = StoredSessionIndicatorSeedReader.Aggregate("ES20261218", TimeFrameType.OneHour,
            new(Date, Start, Start.AddHours(1)), Start.AddHours(1), [Row(100m, 1, 1, Start.AddMinutes(30))])!;
        db.MarketDataDb.GetStoredTradeSessionBarAsync("ES20261218", TimeFrameType.OneHour, Date, Start.AddHours(1), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel?>(saved));
        var market = Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();
        var reader = new StoredSessionIndicatorSeedReader(db, new CmeFuturesMarketSessionCalendar(), new Clock(Start.AddHours(1)),
            NullLogger<StoredSessionIndicatorSeedReader>.Instance, market);
        Assert.Same(saved, Assert.Single(await reader.ReadAsync("ES20261218", TimeFrameType.OneHour, 1, default)));
        await ticks.DidNotReceive().GetRawFuturesTradeWindowAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>());
        await market.DidNotReceive().ReadIndicatorSeedBarsAsync(Arg.Any<string>(), Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TimeFrameType.FifteenMinutes)]
    [InlineData(TimeFrameType.OneHour)]
    [InlineData(TimeFrameType.FourHours)]
    public async Task Local_history_gap_delegates_to_the_native_owner_with_the_requested_timeframe(TimeFrameType frame)
    {
        var db = Substitute.For<IDbContextFactory>();
        var ticks = Substitute.For<ITradeTickQuoteDbContext>();
        db.TradeTickQuoteDb.Returns(ticks);
        ticks.GetRawFuturesTradeWindowAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BootstrapTradeRow>>([]));
        var market = Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel[] expected = [new() { ContractId = "ES20261218", TimeFrame = frame }];
        market.ReadIndicatorSeedBarsAsync("ES20261218", frame, Arg.Any<CancellationToken>()).Returns(Task.FromResult(expected));
        var reader = new StoredSessionIndicatorSeedReader(db, new CmeFuturesMarketSessionCalendar(), new Clock(Start.AddHours(3)),
            NullLogger<StoredSessionIndicatorSeedReader>.Instance, market);
        Assert.Same(expected, await reader.ReadAsync("ES20261218", frame, 48, default));
        await market.Received(1).ReadIndicatorSeedBarsAsync("ES20261218", frame, Arg.Any<CancellationToken>());
        if (frame == TimeFrameType.FourHours)
            await ticks.DidNotReceive().GetRawFuturesTradeWindowAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>());
    }

    static readonly DateOnly Date = new(2026, 11, 4);
    static readonly DateTimeOffset Start = new(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Aggregation_preserves_original_ohlcv_and_lineage_without_duplicate_or_outside_trades()
    {
        var first = Row(100m, 2, 10, Start.AddMinutes(1));
        var last = Row(103m, 3, 11, Start.AddMinutes(59));
        var rows = new[] { last, first, first, Row(999m, 1, 12, Start.AddHours(1)), last with { Action = (short)'C', Price = 1m } };
        var bar = StoredSessionIndicatorSeedReader.Aggregate("ES20261218", TimeFrameType.OneHour,
            new(Date, Start, Start.AddHours(1)), Start.AddHours(1), rows)!;
        Assert.Equal(100m, bar.Open); Assert.Equal(103m, bar.High); Assert.Equal(100m, bar.Low); Assert.Equal(103m, bar.Close);
        Assert.Equal(5m, bar.Volume); Assert.Equal(509m, bar.PriceVolumeSum); Assert.Equal(2, bar.TradeCount);
        Assert.Equal(10, bar.FirstSourceSequence); Assert.Equal(11, bar.LastSourceSequence);
        Assert.Equal(Start.AddMinutes(59), bar.LastMarketEventUtc);
        Assert.True(bar.IsComplete && bar.IsValid);
    }

    [Fact]
    public void Missing_original_trades_never_create_a_fabricated_seed_bar()
        => Assert.Null(StoredSessionIndicatorSeedReader.Aggregate("ES20261218", TimeFrameType.OneHour,
            new(Date, Start, Start.AddHours(1)), Start.AddHours(1), []));

    [Fact]
    public async Task Midnight_window_reads_both_utc_segments_under_the_same_trading_date()
    {
        var db = Substitute.For<IDbContextFactory>();
        var ticks = Substitute.For<ITradeTickQuoteDbContext>();
        db.TradeTickQuoteDb.Returns(ticks);
        ticks.GetRawFuturesTradeWindowAsync(Arg.Any<string>(), Date, Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BootstrapTradeRow>>([Row(100m, 1, 1, Start.AddMinutes(30))]));
        var reader = new StoredSessionIndicatorSeedReader(db, new CmeFuturesMarketSessionCalendar(),
            new Clock(Start.AddHours(3)), NullLogger<StoredSessionIndicatorSeedReader>.Instance);
        var bars = await reader.ReadAsync("ES20261218", TimeFrameType.FourHours, 1, default);
        Assert.Single(bars);
        await ticks.Received(2).GetRawFuturesTradeWindowAsync("ES20261218", Date, Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>());
    }

    static BootstrapTradeRow Row(decimal price, long size, long sequence, DateTimeOffset at)
    {
        var ns = (at.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;
        return new(Date, price, size, sequence, ns, ns, (short)'T', 0, 42, 1);
    }

    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
