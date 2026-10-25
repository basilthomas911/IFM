using TomasAI.IFM.Application.MarketData.Host.Seeding;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using Xunit;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class FourHourDatabentoSeedReplayTests
{
    [Theory]
    [InlineData("2026-10-25T22:00:00Z", "2026-10-23T17:00:00Z")]
    [InlineData("2026-11-01T23:00:00Z", "2026-10-30T17:00:00Z")]
    public async Task Sunday_open_uses_historical_ohlcv_for_all_48_prior_session_bars(string at, string expectedStart)
    {
        var f = Fixture(DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture));
        var bars = await f.Replay.ReadAsync("ES-SEED", default);
        Assert.Equal(48, bars.Length);
        Assert.Equal(DateTimeOffset.Parse(expectedStart, System.Globalization.CultureInfo.InvariantCulture), bars[0].IntervalStartUtc);
        Assert.All(bars, bar => Assert.Empty(new FuturesTradeSessionBarReadModelValidationRules().Execute(bar)));
        Assert.All(bars, bar => { Assert.Equal(101m, bar.Open); Assert.Equal(104m, bar.High); Assert.Equal(99m, bar.Low); Assert.Equal(102m, bar.Close); Assert.Equal(50m, bar.Volume); });
        f.Feeds.DidNotReceive().CreateTickerFeed(Arg.Any<DatabentoFeedOptions>());
        Assert.Single(f.Requests);
        Assert.Equal(HistoricalDataSchema.OhlcvOneMinute, f.Requests[0].Schema);
    }

    [Fact]
    public async Task Sunday_after_open_combines_friday_history_with_a_sunday_live_replay_start()
    {
        var f = Fixture(new(2026, 10, 25, 22, 11, 0, TimeSpan.Zero));
        DatabentoFeedOptions? captured = null;
        f.Feeds.CreateTickerFeed(Arg.Any<DatabentoFeedOptions>()).Returns(call =>
        {
            captured = call.Arg<DatabentoFeedOptions>();
            throw new ReplayBoundaryReached();
        });
        await Assert.ThrowsAsync<ReplayBoundaryReached>(() => f.Replay.ReadAsync("ES-SEED", default));
        Assert.NotNull(captured);
        var replayStart = DateTimeOffset.UnixEpoch.AddTicks((long)(captured.TradeReplayStartTimestampNanoseconds / 100));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 22, 0, 0, TimeSpan.Zero), replayStart);
        Assert.Single(f.Requests);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 17, 10, 0, TimeSpan.Zero), f.Requests[0].StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero), f.Requests[0].EndUtc);
    }

    [Fact]
    public async Task Missing_historical_minute_returns_no_seed_and_never_requests_friday_live_replay()
    {
        var f = Fixture(new(2026, 10, 25, 22, 11, 0, TimeSpan.Zero), omitMinute: true);
        Assert.Empty(await f.Replay.ReadAsync("ES-SEED", default));
        f.Feeds.DidNotReceive().CreateTickerFeed(Arg.Any<DatabentoFeedOptions>());
    }

    [Fact]
    public async Task Historical_budget_failure_and_cancellation_do_not_start_a_native_seed_feed()
    {
        var f = Fixture(new(2026, 10, 25, 22, 0, 0, TimeSpan.Zero));
        f.Provider.EstimateAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HistoricalProviderEstimate(1m, 100, 240, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Replay.ReadAsync("ES-SEED", default));
        await f.Provider.DidNotReceive().OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Replay.ReadAsync("ES-SEED", cancelled.Token));
        f.Feeds.DidNotReceive().CreateTickerFeed(Arg.Any<DatabentoFeedOptions>());
    }

    static (FourHourDatabentoSeedReplay Replay, IDatabentoFeedFactory Feeds,
        IMarketDataHistoricalProvider Provider, List<HistoricalProviderRequest> Requests) Fixture(
        DateTimeOffset now, bool omitMinute = false)
    {
        var provider = Substitute.For<IMarketDataHistoricalProvider>();
        var feeds = Substitute.For<IDatabentoFeedFactory>();
        var requests = new List<HistoricalProviderRequest>();
        provider.EstimateAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HistoricalProviderEstimate(0.001m, 1000, 240, now));
        provider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<HistoricalProviderRequest>();
                requests.Add(request);
                var records = new List<HistoricalProviderRecord>();
                for (var time = request.StartUtc; time < request.EndUtc; time = time.AddMinutes(1))
                    if (!omitMinute || time != request.StartUtc)
                        records.Add(new() { Kind = HistoricalProviderRecordKind.Ohlcv, Symbol = "ESZ6", InstrumentId = "1",
                            EventTimestampUtc = time, Open = 101, High = 104, Low = 99, CloseOrPrice = 102, VolumeOrSize = 10 });
                return new MinuteReader(records);
            });
        var options = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3") with { DataSource = FeedDataSourceMode.DatabentoLive },
            Contracts = [new() { DomainContractId = "ES-SEED", ProviderContractName = "ESZ6", Dataset = "GLBX.MDP3", AssetTypeId = AssetTypeId.Futures }]
        };
        return (new(feeds, options, new CmeFuturesMarketSessionCalendar(), new FixedClock(now), historical: provider), feeds, provider, requests);
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    sealed class ReplayBoundaryReached : Exception;
    sealed class MinuteReader(IReadOnlyList<HistoricalProviderRecord> records) : IHistoricalRecordReader
    {
        bool read;
        public ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (read) return ValueTask.FromResult<HistoricalProviderRecordBatch?>(null);
            read = true;
            return ValueTask.FromResult<HistoricalProviderRecordBatch?>(new(records, 0, "fixture", true));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void Replay_bar_uses_event_time_for_ohlc_and_a_matching_observation_identity()
    {
        var start = new DateTimeOffset(2026, 10, 2, 14, 0, 0, TimeSpan.Zero);
        var window = new MarketSessionBounds(new DateOnly(2026, 10, 2),
            start, start.AddMinutes(5));
        var bucket = new FourHourDatabentoSeedReplay.BarBucket();
        bucket.Add(102m, 2, start.AddMinutes(2), 12);
        bucket.Add(101m, 3, start.AddMinutes(1), 11);
        bucket.Add(103m, 4, start.AddMinutes(3), 13);
        var bar = bucket.ToObservation(MarketSeriesIdentity.ForContract("ES-SEED"),
            "ES-SEED", window, window.EndUtc);

        Assert.Equal(101m, bar.Open);
        Assert.Equal(103m, bar.Close);
        Assert.Equal(101m, bar.Low);
        Assert.Equal(103m, bar.High);
        Assert.Equal(9m, bar.Volume);
        Assert.Equal(3, bar.TradeCount);
        Assert.Equal(11, bar.FirstSourceSequence);
        Assert.Equal(13, bar.LastSourceSequence);
        Assert.Empty(new FuturesTradeSessionBarReadModelValidationRules().Execute(bar));
    }
}
