using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

/// <summary>Qualifies local raw-trade history without substituting close-only or missing data.</summary>
public sealed class StoredSessionIndicatorSeedTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Daily_oscillators_require_every_original_baseline_session(bool omitLastSession)
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
            .Returns(new ValueTask<IReadOnlyList<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesEodObservationReadModel>>(
                omitLastSession ? original[..^1] : original));
        var reader = new StoredSessionIndicatorSeedReader(db, calendar, new Clock(now), NullLogger<StoredSessionIndicatorSeedReader>.Instance, market);
        var bars = await reader.ReadAsync("ES20261218", TimeFrameType.Daily, 48, default);
        await market.DidNotReceive().ReadIndicatorSeedBarsAsync(Arg.Any<string>(), Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>());
        if (omitLastSession)
        {
            Assert.Empty(bars);
            return;
        }
        Assert.Equal(48, bars.Length);
        Assert.All(bars, bar => { Assert.Equal("ES20261218", bar.ContractId); Assert.Equal(series, bar.MarketSeriesIdentity); });
        Assert.Equal(original.Select(value => value.ObservationId), bars.Select(value => value.ObservationId));
        Assert.All(original, value => Assert.Equal("ES.c.0", value.ContractId));
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

    [Fact]
    public async Task Matching_native_coverage_restores_through_the_api_without_rescanning_ticks_or_calling_the_host()
    {
        const string contract = "ES20261218";
        const TimeFrameType frame = TimeFrameType.OneMinute;
        var db = Substitute.For<IDbContextFactory>();
        var ticks = Substitute.For<ITradeTickQuoteDbContext>();
        db.TradeTickQuoteDb.Returns(ticks);
        var market = Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();
        var now = Start.AddHours(15);
        var calendar = new CmeFuturesMarketSessionCalendar();
        var window = Assert.Single(RsiHistoricalSeedWindowModel.Create(frame, 1, now, calendar));
        var saved = StoredSessionIndicatorSeedReader.Aggregate(contract, frame, window, now,
            [Row(100m, 1, 1, window.StartUtc.AddSeconds(10)) with { ValueDate = window.ValueDate }])!;
        var options = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3")
                with { DataSource = FeedDataSourceMode.DatabentoLive },
            Contracts = [new() { DomainContractId = contract, ProviderContractName = "ESZ6", AssetTypeId = AssetTypeId.Futures }]
        };
        db.MarketDataDb.GetQualifiedIndicatorSeedBarsAsync(contract, frame, window.ValueDate,
                Arg.Is<DateTimeOffset[]>(ends => ends.Length == 1 && ends[0] == window.EndUtc),
                "native-databento-seed-v1|9:GLBX.MDP3|ESZ6", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FuturesTradeSessionBarReadModel>>([saved]));

        var restarted = new StoredSessionIndicatorSeedReader(db, calendar, new Clock(now),
            NullLogger<StoredSessionIndicatorSeedReader>.Instance, market, options);
        Assert.Same(saved, Assert.Single(await restarted.ReadAsync(contract, frame, 1, default)));
        Assert.Empty(ticks.ReceivedCalls());
        await market.DidNotReceive().ReadIndicatorSeedBarsAsync(Arg.Any<string>(), Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Complete_four_hour_coverage_restores_on_api_restart_without_a_host_call()
    {
        const string contract = "ES20261218";
        const TimeFrameType frame = TimeFrameType.FourHours;
        var now = new DateTimeOffset(2026, 11, 5, 15, 30, 0, TimeSpan.Zero);
        var calendar = new CmeFuturesMarketSessionCalendar();
        var db = Substitute.For<IDbContextFactory>();
        var market = Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();
        var windows = RsiHistoricalSeedWindowModel.Create(frame, 48, now, calendar);
        var saved = windows.Select(window => StoredSessionIndicatorSeedReader.Aggregate(contract, frame, window, now,
            [Row(100m, 1, 1, window.StartUtc.AddMinutes(1)) with { ValueDate = window.ValueDate }])!).ToArray();
        db.MarketDataDb.GetStoredTradeSessionBarsAsync(contract, frame, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset[]>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<FuturesTradeSessionBarReadModel>>(saved
                .Where(bar => call.Arg<DateTimeOffset[]>().Contains(bar.IntervalEndUtc)).ToArray()));
        db.MarketDataDb.GetStoredTradeSessionBarAsync(contract, frame, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(call => saved.Single(bar => bar.IntervalEndUtc == call.Arg<DateTimeOffset>()));

        var restarted = new StoredSessionIndicatorSeedReader(db, calendar, new Clock(now), NullLogger<StoredSessionIndicatorSeedReader>.Instance, market);
        var restored = await restarted.ReadAsync(contract, frame, 48, default);

        Assert.Equal(windows.Select(window => window.EndUtc), restored.Select(bar => bar.IntervalEndUtc));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Adjacent_short_intervals_keep_each_original_read_boundary(bool includesLegacyRow)
    {
        const string contract = "ES20261218";
        var now = Start.AddHours(15).AddMinutes(30);
        var calendar = new CmeFuturesMarketSessionCalendar();
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.OneMinute, 4, now, calendar);
        var db = Substitute.For<IDbContextFactory>();
        var ticks = Substitute.For<ITradeTickQuoteDbContext>();
        db.TradeTickQuoteDb.Returns(ticks);
        var rows = windows.Select((window, index) => Row(100m, 1, index + 1, window.StartUtc.AddSeconds(10)) with
        {
            AggregationTimeUtc = TimeOnly.FromDateTime(window.StartUtc.UtcDateTime.AddSeconds(20))
        }).Append(Row(999m, 1, 5, windows[0].StartUtc.AddSeconds(15)) with
        {
            AggregationTimeUtc = includesLegacyRow ? null : TimeOnly.FromDateTime(windows[^1].StartUtc.UtcDateTime)
        }).ToArray();
        ticks.GetRawFuturesTradeWindowAsync(contract, Date, new TimeOnly(15, 25), new TimeOnly(15, 31), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BootstrapTradeRow>>(rows));
        if (includesLegacyRow)
        {
            for (var index = 0; index < windows.Length; index++)
            {
                var window = windows[index];
                var lower = TimeOnly.FromDateTime(window.StartUtc.UtcDateTime.AddMinutes(-1));
                var upper = TimeOnly.FromDateTime(window.EndUtc.UtcDateTime.AddMinutes(1));
                ticks.GetRawFuturesTradeWindowAsync(contract, Date, lower, upper, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyList<BootstrapTradeRow>>([rows[index]]));
            }
        }

        var reader = new StoredSessionIndicatorSeedReader(db, calendar, new Clock(now), NullLogger<StoredSessionIndicatorSeedReader>.Instance);
        var bars = await reader.ReadAsync(contract, TimeFrameType.OneMinute, 4, default);

        Assert.Equal(4, bars.Length);
        Assert.All(bars, bar => { Assert.Equal(1, bar.TradeCount); Assert.Equal(100m, bar.High); });
        await ticks.Received(includesLegacyRow ? 5 : 1).GetRawFuturesTradeWindowAsync(contract, Date,
            Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>());
    }

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
