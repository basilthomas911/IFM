using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Full original/current orchestration over identical recorded models and yielding command acknowledgements. Measures orchestration and eliminated work, excluding database, broker and indicator calculation latency.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig)), InvocationCount(1)]
public class ApiServerPerformanceWarmupBenchmarks
{
    [Params("Stale", "Retry", "Independent", "SameKey")] public string Scenario { get; set; } = "Retry";
    Fixture before = null!, after = null!;
    [IterationSetup] public void Prepare() { before = new(Scenario, false); after = new(Scenario, true); }
    [Benchmark(Baseline = true), BenchmarkCategory("13-14-18-WarmupOrchestration")]
    public Task OriginalBefore() => before.RunAsync();
    [Benchmark, BenchmarkCategory("13-14-18-WarmupOrchestration")]
    public Task CurrentAfter() => after.RunAsync();

    sealed class Fixture
    {
        readonly string scenario;
        readonly bool candidate;
        readonly DateOnly date = new(2026, 11, 6);
        readonly DateTimeOffset now = new(2026, 11, 6, 0, 15, 0, TimeSpan.Zero);
        readonly StoredSessionIndicatorWarmup current;
        readonly ApiServerPerformanceWarmupBaseline original;
        readonly TimeFrameType frame;
        readonly Func<string, DateOnly, TimeFrameType, CancellationToken, bool, Task<bool>> stale;
        int commands, macdCalls, bandReads, requestedBars;

        public Fixture(string scenario, bool candidate)
        {
            this.scenario = scenario; this.candidate = candidate;
            frame = scenario == "Stale" ? TimeFrameType.FifteenMinutes : TimeFrameType.OneMinute;
            var calendar = new CmeFuturesMarketSessionCalendar(); var clock = new Clock(now);
            var windows = RsiHistoricalSeedWindowModel.Create(frame, 48, now, calendar);
            var corpus = Enumerable.Range(0, 8).ToDictionary(index => "ES-warmup-perf-" + index, contractIndex =>
            {
                var contract = "ES-warmup-perf-" + contractIndex;
                var series = MarketSeriesIdentity.ForContract(contract);
                return windows.Select((window, index) => new FuturesTradeSessionBarReadModel
                {
                    MarketSeriesIdentity = series, ContractId = contract, TimeFrame = frame, ValueDate = window.ValueDate,
                    ObservationId = FuturesTradeSessionBarId.Create(series, frame, window.EndUtc, index + 1),
                    IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
                    Open = 5000, High = 5001, Low = 4999, Close = 5000, Volume = 1, TradeCount = 1, PriceVolumeSum = 5000,
                    FirstSourceSequence = index + 1, LastSourceSequence = index + 1,
                    FirstMarketEventUtc = window.StartUtc, LastMarketEventUtc = window.EndUtc.AddMilliseconds(-1),
                    CalculatedAtUtc = now, IsComplete = true, IsValid = true, SchemaVersion = 1, CalculationVersion = "api-perf-v1",
                    CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
                }).ToArray();
            });
            var databases = Substitute.For<IDbContextFactory>(); var db = databases.MarketDataDb;
            db.GetStoredTradeSessionBarsAsync(Arg.Any<string>(), frame, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset[]>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var ends = call.Arg<DateTimeOffset[]>(); Interlocked.Add(ref requestedBars, ends.Length);
                    return Task.FromResult<IReadOnlyList<FuturesTradeSessionBarReadModel>>(corpus[call.Arg<string>()].Where(bar => ends.Contains(bar.IntervalEndUtc)).ToArray());
                });
            db.GetStoredTradeSessionBarAsync(Arg.Any<string>(), frame, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                .Returns(call => { Interlocked.Increment(ref requestedBars); return Task.FromResult(corpus[call.Arg<string>()].SingleOrDefault(bar => bar.IntervalEndUtc == call.Arg<DateTimeOffset>())); });
            MarketAnalyticsSignalMetadata Metadata(string contract, MarketAnalyticsSignalKind kind, DateTimeOffset asOf) => new()
            {
                ContractId = contract, SignalKey = new(MarketSeriesIdentity.ForContract(contract), kind, frame, "perf"),
                ValueDate = date, MarketDataAsOfUtc = asOf, CalculatedAtUtc = now, SourceSequence = 48,
                IsValid = true, SchemaVersion = 1, CalculationVersion = "api-perf-v1"
            };
            db.GetLastFuturesAdxSignalAsync(Arg.Any<string>(), date, frame, 14, Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult<FuturesAdxSignalReadModel?>(new() { ContractId = call.Arg<string>(), IsWarm = true, Metadata = Metadata(call.Arg<string>(), MarketAnalyticsSignalKind.Adx, now.AddDays(-1)) }));
            db.GetLastFuturesAtrSignalAsync(Arg.Any<string>(), date, frame, 14, Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult<FuturesAtrSignalReadModel?>(new() { ContractId = call.Arg<string>(), TimePeriod = frame, ValueDate = date, IsWarm = true, AtrValue = 2, AtrBaseline = 2, AtrRatio = 1, Metadata = Metadata(call.Arg<string>(), MarketAnalyticsSignalKind.Atr, now.AddDays(-1)) }));
            db.GetLastFuturesMacdSignalAsync(Arg.Any<string>(), date, frame, 9, 12, 26, Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult<FuturesMacdSignalReadModel?>(new() { ContractId = call.Arg<string>(), IsWarm = true, Metadata = Metadata(call.Arg<string>(), MarketAnalyticsSignalKind.Macd, now.AddDays(-1)) }));
            db.GetLastFuturesRsiSignalAsync(Arg.Any<string>(), date, frame, 14, Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult<FuturesRsiSignalReadModel?>(new() { ContractId = call.Arg<string>(), IsWarm = true, Metadata = Metadata(call.Arg<string>(), MarketAnalyticsSignalKind.Rsi, now.AddDays(-1)) }));
            db.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(Arg.Any<MarketSeriesIdentity>(), date, frame, Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var contract = call.Arg<MarketSeriesIdentity>().ContractId;
                    var asOf = scenario == "Stale" && Interlocked.Increment(ref bandReads) == 1 ? now.AddDays(-1) : now;
                    return Task.FromResult<FuturesBbSignalReadModel?>(new() { Metadata = Metadata(contract, MarketAnalyticsSignalKind.BollingerBand, asOf), IsWarm = true, Width20 = 80, Width20Ratio = 1.25m, Position20 = .6m });
                });
            var actors = Substitute.For<IActorService>();
            actors.RequestAsync<StartFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(Arg.Any<StartFuturesRsiSignalCommand>(), Arg.Any<CancellationToken>()).Returns(_ => ReplyAsync(false));
            actors.RequestAsync<StartFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(Arg.Any<StartFuturesAdxSignalCommand>(), Arg.Any<CancellationToken>()).Returns(_ => ReplyAsync(false));
            actors.RequestAsync<StartFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(Arg.Any<StartFuturesAtrSignalCommand>(), Arg.Any<CancellationToken>()).Returns(_ => ReplyAsync(false));
            actors.RequestAsync<StartFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(Arg.Any<StartFuturesMacdSignalCommand>(), Arg.Any<CancellationToken>()).Returns(_ => ReplyAsync(true));
            var market = Substitute.For<IMarketDataApi>();
            current = new(new(databases, calendar, clock, NullLogger<StoredSessionIndicatorSeedReader>.Instance), actors, market, databases, calendar, clock, NullLogger<StoredSessionIndicatorWarmup>.Instance);
            original = new(new(databases, calendar, clock, NullLogger<ApiServerPerformanceSeedReaderBaseline>.Instance), actors, market, databases, calendar, clock, NullLogger<ApiServerPerformanceWarmupBaseline>.Instance);
            var owner = candidate ? (object)current : original;
            stale = owner.GetType().GetMethod("WarmFrameAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .CreateDelegate<Func<string, DateOnly, TimeFrameType, CancellationToken, bool, Task<bool>>>(owner);
        }

        async ValueTask<ServiceResult<Guid>> ReplyAsync(bool macd)
        {
            Interlocked.Increment(ref commands);
            await Task.Yield();
            if (macd && Interlocked.Increment(ref macdCalls) == 1 && scenario == "Retry") throw new IOException("Injected one-time MACD failure.");
            return new ServiceOk<Guid>(Guid.Empty);
        }

        public async Task RunAsync()
        {
            if (scenario == "Stale")
            {
                if (!await stale("ES-warmup-perf-0", date, frame, default, false)) throw new InvalidOperationException("Stale warmup failed.");
                if (requestedBars != (candidate ? 48 : 69)) throw new InvalidOperationException($"Unexpected bars requested: {requestedBars}.");
            }
            else if (scenario == "Retry")
            {
                if (await ShortAsync(0) || !await ShortAsync(0)) throw new InvalidOperationException("Retry qualification failed.");
                if (commands != (candidate ? 6 : 10)) throw new InvalidOperationException($"Unexpected retry commands: {commands}.");
            }
            else
            {
                var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => ShortAsync(scenario == "SameKey" ? 0 : index)));
                if (results.Any(value => !value) || commands != (scenario == "SameKey" ? 5 : 40)) throw new InvalidOperationException("Ownership qualification failed.");
            }
        }
        Task<bool> ShortAsync(int index) => candidate
            ? current.WarmShortFrameAsync("ES-warmup-perf-" + index, date, frame, default)
            : original.WarmShortFrameAsync("ES-warmup-perf-" + index, date, frame, default);
    }
    sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}

