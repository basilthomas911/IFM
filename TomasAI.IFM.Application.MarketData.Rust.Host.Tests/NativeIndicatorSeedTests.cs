using System.Diagnostics;
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
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Tests;

/// <summary>Qualifies original minute coverage and the no-purchase ceiling in the actual native Host seed implementation.</summary>
public sealed class NativeIndicatorSeedTests(ITestOutputHelper output)
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
            bars.Add((string.Empty, window.EndUtc), bucket.ToObservation(series, Contract, window, now, TimeFrameType.FourHours));
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
    [InlineData(TimeFrameType.OneMinute)]
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
    public async Task Fifteen_second_history_uses_original_trades_to_build_48_bars()
    {
        var provider = Provider(0m);
        provider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<HistoricalProviderRequest>();
                Assert.Equal(HistoricalDataSchema.Trades, request.Schema);
                return new Reader(Enumerable.Range(0, (int)(request.EndUtc - request.StartUtc).TotalSeconds / 15)
                    .Select(i => new HistoricalProviderRecord
                    {
                        Kind = HistoricalProviderRecordKind.Trade, Symbol = "ESZ6", InstrumentId = "42",
                        EventTimestampUtc = request.StartUtc.AddSeconds(i * 15 + 1), CloseOrPrice = 100m + i,
                        VolumeOrSize = 2, SourceSequence = i + 1
                    }).ToArray());
            });
        var bars = await Create(provider).ReadAsync(Contract, TimeFrameType.FifteenSeconds, default);

        Assert.Equal(48, bars.Length);
        Assert.All(bars, bar =>
        {
            Assert.Equal(TimeFrameType.FifteenSeconds, bar.TimeFrame);
            Assert.True(bar.IsComplete && bar.IsValid);
            Assert.Equal(1, bar.TradeCount);
            Assert.Equal(bar.Close * 2, bar.PriceVolumeSum);
        });
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

    [Fact]
    public async Task Restart_reacquires_only_the_hole_in_a_completed_historical_range()
    {
        var (database, bars) = Cache();
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.OneMinute, 48, Now, new CmeFuturesMarketSessionCalendar());
        var missing = windows[20];
        var partialProvider = Provider(0m);
        partialProvider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new Reader(Minutes(call.Arg<HistoricalProviderRequest>())
                .Where(record => record.EventTimestampUtc != missing.StartUtc).ToArray()));

        Assert.Empty(await Create(partialProvider, database).ReadAsync(Contract, TimeFrameType.OneMinute, default));
        Assert.Equal(47, bars.Count);
        var retryProvider = Provider(0m);
        retryProvider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new Reader(Minutes(call.Arg<HistoricalProviderRequest>())));

        var restored = await Create(retryProvider, database).ReadAsync(Contract, TimeFrameType.OneMinute, default);
        Assert.Equal(windows.Select(window => window.EndUtc), restored.Select(bar => bar.IntervalEndUtc));
        await retryProvider.Received(1).OpenRangeAsync(Arg.Is<HistoricalProviderRequest>(request =>
            request.StartUtc == missing.StartUtc && request.EndUtc == missing.EndUtc), Arg.Any<int>(), Arg.Any<CancellationToken>());
        Assert.Equal(48, bars.Count);
    }

    [Fact]
    public async Task Provider_remapping_never_reuses_the_prior_cache_and_keeps_observation_ids_stable()
    {
        var (database, bars) = Cache();
        var provider = Provider(0m);
        provider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<HistoricalProviderRequest>();
                var price = request.Symbols[0] == "ESZ6" ? 100 : 101;
                return new Reader(Minutes(request).Select(row => row with
                    { Symbol = request.Symbols[0], CloseOrPrice = price }).ToArray());
            });

        var original = await Create(provider, database).ReadAsync(Contract, TimeFrameType.OneMinute, default);
        var remapped = await Create(provider, database, symbol: "ESZ26").ReadAsync(Contract, TimeFrameType.OneMinute, default);

        Assert.Equal(48, original.Length);
        Assert.Equal(original.Select(bar => bar.ObservationId), remapped.Select(bar => bar.ObservationId));
        Assert.All(original, bar => Assert.Equal(100, bar.Close));
        Assert.All(remapped, bar => Assert.Equal(101, bar.Close));
        Assert.Equal(96, bars.Count);
        await provider.Received().OpenRangeAsync(Arg.Is<HistoricalProviderRequest>(request => request.Symbols[0] == "ESZ26"),
            Arg.Any<int>(), Arg.Any<CancellationToken>());
        Assert.Equal(original, await Create(Provider(0m), database).ReadAsync(Contract, TimeFrameType.OneMinute, default));
    }

    [Fact]
    public async Task A_live_replay_without_a_final_marker_does_not_persist_apparently_complete_bars()
    {
        var (database, bars) = Cache();
        var now = new DateTimeOffset(2026, 11, 5, 15, 30, 0, TimeSpan.Zero);
        var replay = Substitute.For<INativeSeedReplaySource>();
        replay.Open(Arg.Any<DatabentoContractRegistration>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Reader(Enumerable.Range(0, 48).Select(index => new HistoricalProviderRecord
            {
                Kind = HistoricalProviderRecordKind.Trade, Symbol = "ESZ6", InstrumentId = "42",
                EventTimestampUtc = now.AddMinutes(index - 48).AddSeconds(1), CloseOrPrice = 100, VolumeOrSize = 1,
                SourceSequence = index + 1
            }).ToArray(), complete: false));

        await Assert.ThrowsAsync<InvalidDataException>(() => Create(Provider(0m), database, replay, now)
            .ReadAsync(Contract, TimeFrameType.OneMinute, default));
        Assert.Empty(bars);
    }


    [Fact]
    public async Task Short_frame_replay_component_timings()
    {
        var now = new DateTimeOffset(2026, 11, 5, 15, 30, 0, TimeSpan.Zero);
        var start = now.AddMinutes(-48);
        var rows = Enumerable.Range(0, 48 * 60 * 8).Select(i => new HistoricalProviderRecord
        {
            Kind = HistoricalProviderRecordKind.Trade,
            Symbol = "ESZ6", InstrumentId = "42", EventTimestampUtc = start.AddMilliseconds(i * 125L),
            CloseOrPrice = 100 + i % 10,
            VolumeOrSize = 1,
            SourceSequence = i + 1
        }).ToArray();
        for (var sample = 0; sample <= 5; sample++)
        {
            var opens = 0;
            var delivered = 0;
            var replay = Substitute.For<INativeSeedReplaySource>();
            replay.Open(Arg.Any<DatabentoContractRegistration>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                Interlocked.Increment(ref opens);
                var offset = checked((int)((call.Arg<DateTimeOffset>() - start).TotalSeconds * 8));
                var records = rows[offset..];
                Interlocked.Add(ref delivered, records.Length);
                return new ReplayReader(records);
            });
            var host = Create(Provider(0m), replay: replay, now: now);
            var started = Stopwatch.GetTimestamp();
            var results = await Task.WhenAll(
                host.ReadAsync(Contract, TimeFrameType.FifteenSeconds, default),
                host.ReadAsync(Contract, TimeFrameType.OneMinute, default));
            var elapsed = Stopwatch.GetElapsedTime(started);
            Assert.All(results, frame => Assert.Equal(48, frame.Length));
            Assert.Equal(rows.Length, results[1].Sum(bar => bar.TradeCount));
            Assert.Equal(12 * 60 * 8, results[0].Sum(bar => bar.TradeCount));
            if (Environment.GetEnvironmentVariable("IFM_EXPECT_SHARED_SEED") == "1")
            {
                Assert.Equal(1, opens);
                Assert.Equal(rows.Length, delivered);
            }
            if (sample > 0)
                output.WriteLine($"SEED_TIMING sample={sample} ms={elapsed.TotalMilliseconds:F3} opens={opens} records={delivered}");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Short_frame_hole_or_cancel_does_not_prevent_the_other_frame_from_completing(bool cancel)
    {
        var now = new DateTimeOffset(2026, 11, 5, 15, 30, 0, TimeSpan.Zero);
        var replay = Substitute.For<INativeSeedReplaySource>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var records = Enumerable.Range(0, 48 * 4).Where(i => i != 48 * 4 - 2)
            .Select(i => new HistoricalProviderRecord
            {
                Kind = HistoricalProviderRecordKind.Trade, Symbol = "ESZ6", InstrumentId = "42",
                EventTimestampUtc = now.AddMinutes(-48).AddSeconds(15 * i + 1),
                CloseOrPrice = 100, VolumeOrSize = 1, SourceSequence = i + 1
            }).ToArray();
        replay.Open(Arg.Any<DatabentoContractRegistration>(), now.AddMinutes(-48), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new GatedReplayReader(records, entered, release));
        var host = Create(Provider(0m), replay: replay, now: now);
        using var cancellation = new CancellationTokenSource();
        var shortFrame = host.ReadAsync(Contract, TimeFrameType.FifteenSeconds, cancellation.Token);
        var minute = host.ReadAsync(Contract, TimeFrameType.OneMinute, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (cancel) await cancellation.CancelAsync();
        release.TrySetResult();

        Assert.Equal(48, (await minute).Length);
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => shortFrame);
        else
            Assert.Empty(await shortFrame);
        replay.Received(1).Open(Arg.Any<DatabentoContractRegistration>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restart_reuses_a_short_frame_and_acquires_only_newly_closed_intervals()
    {
        var expectRetainedCoverage = Environment.GetEnvironmentVariable("IFM_EXPECT_SHORT_RESTART_CACHE") != "0";
        var (database, bars) = Cache();
        var now = new DateTimeOffset(2026, 11, 5, 15, 30, 0, TimeSpan.Zero);
        var replay = Substitute.For<INativeSeedReplaySource>();
        replay.Open(Arg.Any<DatabentoContractRegistration>(), now.AddMinutes(-48), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Reader(Enumerable.Range(0, 48).Select(i => Trade(now.AddMinutes(-48 + i).AddSeconds(1), i + 1)).ToArray()));
        var original = await Create(Provider(0m), database, replay, now).ReadAsync(Contract, TimeFrameType.OneMinute, default);
        Assert.Equal(48, original.Length);
        Assert.Equal(expectRetainedCoverage ? 48 : 0, bars.Count);

        var advanced = now.AddMinutes(1);
        var start = expectRetainedCoverage ? now : advanced.AddMinutes(-48);
        var records = expectRetainedCoverage ? 1 : 48;
        replay.Open(Arg.Any<DatabentoContractRegistration>(), start, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Reader(Enumerable.Range(0, records)
                .Select(index => Trade(start.AddMinutes(index).AddSeconds(1), 50 - records + index)).ToArray()));
        var began = Stopwatch.GetTimestamp();
        var restarted = await Create(Provider(0m), database, replay, advanced).ReadAsync(Contract, TimeFrameType.OneMinute, default);
        output.WriteLine($"RESTART_TIMING ms={Stopwatch.GetElapsedTime(began).TotalMilliseconds:F3} records={records}");
        Assert.Equal(48, restarted.Length);
        var overlapping = expectRetainedCoverage ? original.Skip(1) :
            original.Skip(1).Select(bar => bar with { CalculatedAtUtc = advanced });
        Assert.Equal(overlapping, restarted.Take(47));
        Assert.Equal(advanced, restarted[^1].IntervalEndUtc);
        replay.Received(1).Open(Arg.Any<DatabentoContractRegistration>(), start, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());

        static HistoricalProviderRecord Trade(DateTimeOffset at, int sequence) => new()
        {
            Kind = HistoricalProviderRecordKind.Trade, Symbol = "ESZ6", InstrumentId = "42",
            EventTimestampUtc = at, CloseOrPrice = 100, VolumeOrSize = 1, SourceSequence = sequence
        };
    }

    sealed class GatedReplayReader(HistoricalProviderRecord[] records,
        TaskCompletionSource entered, TaskCompletionSource release) : IHistoricalRecordReader
    {
        bool read;
        public async ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken cancellationToken)
        {
            if (read) return null;
            read = true;
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return new(records, 0, "fixture", true);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    sealed class ReplayReader(HistoricalProviderRecord[] records) : IHistoricalRecordReader
    {
        bool read;
        public async ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken cancellationToken)
        {
            if (read) return null;
            read = true;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return new(records, 0, "fixture", true);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    static IMarketDataHistoricalProvider Provider(decimal cost)
    {
        var provider = Substitute.For<IMarketDataHistoricalProvider>();
        provider.EstimateAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HistoricalProviderEstimate(cost, 1000, 100, Now));
        return provider;
    }

    static (IDbContextFactory Database, Dictionary<(string SourceVersion, DateTimeOffset End), FuturesTradeSessionBarReadModel> Bars) Cache()
    {
        var bars = new Dictionary<(string SourceVersion, DateTimeOffset End), FuturesTradeSessionBarReadModel>();
        var database = Substitute.For<IDbContextFactory>();
        var market = Substitute.For<IMarketDataDbContext>();
        database.MarketDataDb.Returns(market);
        market.GetQualifiedIndicatorSeedBarsAsync(Contract, Arg.Any<TimeFrameType>(), Arg.Any<DateOnly>(),
                Arg.Any<DateTimeOffset[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<FuturesTradeSessionBarReadModel>>(call.Arg<DateTimeOffset[]>()
                .Select(end => bars.GetValueOrDefault((string.Empty, end)) ?? bars.GetValueOrDefault((call.ArgAt<string>(4), end)))
                .OfType<FuturesTradeSessionBarReadModel>()
                .Where(bar => bar.ValueDate == call.Arg<DateOnly>() && bar.TimeFrame == call.Arg<TimeFrameType>())
                .ToArray()));
        market.StoreQualifiedIndicatorSeedBarsAsync(Arg.Any<string>(), Arg.Any<FuturesTradeSessionBarReadModel[]>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                foreach (var bar in call.Arg<FuturesTradeSessionBarReadModel[]>())
                    bars.TryAdd((call.Arg<string>(), bar.IntervalEndUtc), bar);
                return Task.CompletedTask;
            });
        return (database, bars);
    }

    static NativeFuturesSeedReplay Create(IMarketDataHistoricalProvider provider, IDbContextFactory? database = null,
        INativeSeedReplaySource? replay = null, DateTimeOffset? now = null, string symbol = "ESZ6") => new(replay!, new()
    {
        FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3") with { DataSource = FeedDataSourceMode.DatabentoLive },
        Contracts = [new() { DomainContractId = "ES20261218", ProviderContractName = symbol, AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3" }]
    }, new CmeFuturesMarketSessionCalendar(), new Clock(now ?? Now), historical: provider, databases: database);

    static HistoricalProviderRecord[] Minutes(HistoricalProviderRequest request) => Enumerable.Range(0, (int)(request.EndUtc - request.StartUtc).TotalMinutes)
        .Select(i => new HistoricalProviderRecord { Kind = HistoricalProviderRecordKind.Ohlcv, Symbol = "ESZ6", InstrumentId = "42", EventTimestampUtc = request.StartUtc.AddMinutes(i), Open = 100, High = 101, Low = 99, CloseOrPrice = 100, VolumeOrSize = 10 }).ToArray();

    sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    sealed class Reader(HistoricalProviderRecord[] records, bool complete = true) : IHistoricalRecordReader
    {
        bool read;
        public ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken cancellationToken)
        {
            if (read) return ValueTask.FromResult<HistoricalProviderRecordBatch?>(null);
            read = true;
            return ValueTask.FromResult<HistoricalProviderRecordBatch?>(new(records, 0, "fixture", complete));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
