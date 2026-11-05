using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.Rust.Host.Seeding;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Tests;

/// <summary>Qualifies original minute coverage and the no-purchase ceiling in the actual native Host seed implementation.</summary>
public sealed class NativeIndicatorSeedTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 1, 22, 0, 0, TimeSpan.Zero);
    const string Contract = "ES20261218";

    [Fact]
    public async Task Concurrent_cold_four_hour_requests_seed_once_and_a_new_host_reuses_the_bars()
    {
        var (database, bars) = Cache();
        var provider = Provider(0m);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitThenReadAsync(call.Arg<HistoricalProviderRequest>()));
        var host = Create(provider, database);
        var cold = host.ReadAsync(Contract, TimeFrameType.FourHours, default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var concurrent = host.ReadAsync(Contract, TimeFrameType.FourHours, default);
        release.SetResult();

        var original = await cold;
        Assert.Equal(48, original.Length);
        Assert.Equal(original, await concurrent);
        Assert.Equal(48, bars.Count);
        var acquired = provider.ReceivedCalls().Where(call => call.GetMethodInfo().Name == nameof(provider.OpenRangeAsync))
            .Select(call => (HistoricalProviderRequest)call.GetArguments()[0]!).ToArray();
        Assert.NotEmpty(acquired);
        Assert.Equal(acquired.Length, acquired.DistinctBy(request => (request.StartUtc, request.EndUtc)).Count());

        var restartedProvider = Provider(0m);
        Assert.Equal(original, await Create(restartedProvider, database).ReadAsync(Contract, TimeFrameType.FourHours, default));
        Assert.Empty(restartedProvider.ReceivedCalls());

        async ValueTask<IHistoricalRecordReader> WaitThenReadAsync(HistoricalProviderRequest request)
        {
            started.TrySetResult();
            await release.Task;
            return new Reader(Minutes(request));
        }
    }

    [Fact]
    public async Task Same_day_restart_replays_only_the_missing_four_hour_interval()
    {
        var (database, bars) = Cache();
        var now = new DateTimeOffset(2026, 11, 5, 15, 30, 0, TimeSpan.Zero);
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.FourHours, 48, now, new CmeFuturesMarketSessionCalendar());
        var series = MarketSeriesIdentity.ForContract(Contract);
        foreach (var window in windows[..^1])
        {
            var bucket = new NativeFuturesSeedReplay.BarBucket();
            bucket.Add(100, 1, window.StartUtc.AddMinutes(1), 1);
            bars.Add(window.EndUtc, bucket.ToObservation(series, Contract, window, now, TimeFrameType.FourHours));
        }
        var missing = windows[^1];
        var replay = Substitute.For<INativeSeedReplaySource>();
        replay.Open(Arg.Any<DatabentoContractRegistration>(), missing.StartUtc, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Reader([
                new HistoricalProviderRecord { Kind = HistoricalProviderRecordKind.Trade, Symbol = "ESZ6", InstrumentId = "42", EventTimestampUtc = missing.StartUtc.AddMinutes(1),
                    CloseOrPrice = 105, VolumeOrSize = 2, SourceSequence = 10 }
            ]));
        var historical = Provider(0m);

        var restored = await Create(historical, database, replay, now).ReadAsync(Contract, TimeFrameType.FourHours, default);

        Assert.Equal(windows.Select(window => window.EndUtc), restored.Select(bar => bar.IntervalEndUtc));
        Assert.Equal(105, restored[^1].Close);
        Assert.Empty(historical.ReceivedCalls());
        replay.Received(1).Open(Arg.Any<DatabentoContractRegistration>(), missing.StartUtc, TimeSpan.FromMinutes(2), Arg.Any<CancellationToken>());
        Assert.Equal(restored, await Create(historical, database, now: now).ReadAsync(Contract, TimeFrameType.FourHours, default));
    }

    [Fact]
    public async Task Failed_live_replay_preserves_historical_seed_for_the_next_start()
    {
        var (database, bars) = Cache();
        var now = new DateTimeOffset(2026, 11, 5, 15, 30, 0, TimeSpan.Zero);
        var calendar = new CmeFuturesMarketSessionCalendar();
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.FourHours, 48, now, calendar);
        var current = windows.Where(window => window.ValueDate == calendar.GetValueDate(now)).ToArray();
        var provider = Provider(0m);
        provider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new Reader(Minutes(call.Arg<HistoricalProviderRequest>())));
        var failedReplay = Substitute.For<INativeSeedReplaySource>();
        failedReplay.Open(Arg.Any<DatabentoContractRegistration>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new TimeoutException("Replay did not complete."));

        await Assert.ThrowsAsync<TimeoutException>(() => Create(provider, database, failedReplay, now).ReadAsync(Contract, TimeFrameType.FourHours, default));

        Assert.Equal(48 - current.Length, bars.Count);
        var retryProvider = Provider(0m);
        var replay = Substitute.For<INativeSeedReplaySource>();
        replay.Open(Arg.Any<DatabentoContractRegistration>(), current[0].StartUtc, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Reader(current.Select((window, index) => new HistoricalProviderRecord
            {
                Kind = HistoricalProviderRecordKind.Trade, Symbol = "ESZ6", InstrumentId = "42", EventTimestampUtc = window.StartUtc.AddMinutes(1),
                CloseOrPrice = 105, VolumeOrSize = 2, SourceSequence = index + 1,
            }).ToArray()));

        Assert.Equal(48, (await Create(retryProvider, database, replay, now).ReadAsync(Contract, TimeFrameType.FourHours, default)).Length);
        Assert.Equal(48, bars.Count);
        Assert.Empty(retryProvider.ReceivedCalls());
    }

    [Theory]
    [InlineData(TimeFrameType.FifteenMinutes)]
    [InlineData(TimeFrameType.OneHour)]
    [InlineData(TimeFrameType.FourHours)]
    public async Task Original_minutes_reconstruct_all_48_calendar_intervals(TimeFrameType frame)
    {
        var provider = Provider(0m);
        provider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new Reader(Minutes(call.Arg<HistoricalProviderRequest>())));
        var bars = await Create(provider).ReadAsync("ES20261218", frame, default);
        Assert.Equal(48, bars.Length);
        Assert.All(bars, bar => { Assert.Equal(frame, bar.TimeFrame); Assert.True(bar.IsComplete && bar.IsValid); Assert.Equal(101m, bar.High); Assert.Equal(99m, bar.Low); Assert.Equal(100m, bar.Close); });
        Assert.Equal(RsiHistoricalSeedWindowModel.Create(frame, 48, Now, new CmeFuturesMarketSessionCalendar()).Select(w => w.EndUtc), bars.Select(b => b.IntervalEndUtc));
    }

    [Fact]
    public async Task Any_nonzero_estimate_blocks_the_download()
    {
        var provider = Provider(0.01m);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(provider).ReadAsync("ES20261218", TimeFrameType.OneHour, default));
        Assert.Contains("no data was purchased", error.Message);
        await provider.DidNotReceive().OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_original_minute_cannot_qualify_a_complete_seed()
    {
        var provider = Provider(0m);
        provider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new Reader(Minutes(call.Arg<HistoricalProviderRequest>()).Skip(1).ToArray()));
        Assert.Empty(await Create(provider).ReadAsync("ES20261218", TimeFrameType.OneHour, default));
    }

    static IMarketDataHistoricalProvider Provider(decimal cost)
    {
        var provider = Substitute.For<IMarketDataHistoricalProvider>();
        provider.EstimateAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HistoricalProviderEstimate(cost, 1000, 100, Now));
        return provider;
    }

    static (IDbContextFactory Database, Dictionary<DateTimeOffset, FuturesTradeSessionBarReadModel> Bars) Cache()
    {
        var bars = new Dictionary<DateTimeOffset, FuturesTradeSessionBarReadModel>();
        var database = Substitute.For<IDbContextFactory>();
        var market = Substitute.For<IMarketDataDbContext>();
        database.MarketDataDb.Returns(market);
        market.GetStoredTradeSessionBarAsync(Contract, TimeFrameType.FourHours, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(bars.GetValueOrDefault(call.Arg<DateTimeOffset>())));
        ((IHistoricalObservationStore)market).TryWriteObservationAsync(Arg.Any<FuturesTradeSessionBarReadModel>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var bar = call.Arg<FuturesTradeSessionBarReadModel>();
                return ValueTask.FromResult(bars.TryAdd(bar.IntervalEndUtc, bar));
            });
        return (database, bars);
    }

    static NativeFuturesSeedReplay Create(IMarketDataHistoricalProvider provider, IDbContextFactory? database = null,
        INativeSeedReplaySource? replay = null, DateTimeOffset? now = null) => new(replay!, new()
    {
        FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3") with { DataSource = FeedDataSourceMode.DatabentoLive },
        Contracts = [new() { DomainContractId = "ES20261218", ProviderContractName = "ESZ6", AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3" }]
    }, new CmeFuturesMarketSessionCalendar(), new Clock(now ?? Now), historical: provider, databases: database);

    static HistoricalProviderRecord[] Minutes(HistoricalProviderRequest request) => Enumerable.Range(0, (int)(request.EndUtc - request.StartUtc).TotalMinutes)
        .Select(i => new HistoricalProviderRecord { Kind = HistoricalProviderRecordKind.Ohlcv, Symbol = "ESZ6", InstrumentId = "42", EventTimestampUtc = request.StartUtc.AddMinutes(i), Open = 100, High = 101, Low = 99, CloseOrPrice = 100, VolumeOrSize = 10 }).ToArray();

    sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    sealed class Reader(HistoricalProviderRecord[] records) : IHistoricalRecordReader
    {
        bool read;
        public ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken cancellationToken)
        {
            if (read) return ValueTask.FromResult<HistoricalProviderRecordBatch?>(null);
            read = true;
            return ValueTask.FromResult<HistoricalProviderRecordBatch?>(new(records, 0, "fixture", true));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
