using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>One monitored quote among a bounded resident population, with no deadline due.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceExpiryBenchmarks
{
    [Params(1, 64, 1024, 4096)] public int Residents { get; set; }
    ApiServerPerformanceExpiryBaseline before = null!;
    TradeRealtimeCacheRegistry after = null!;
    [GlobalSetup]
    public void Setup()
    {
        var clock = new Clock();
        var date = new DateOnly(2026, 11, 7);
        before = new(clock); after = new(clock);
        for (var index = 0; index < Residents; index++)
        {
            var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501 + index),
                StrategyKind = TradeStrategyKind.IronCondor,
                Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "option-" + index, SignedQuantity = 1 }] };
            before.Register(trade, date); after.Register(trade, date);
        }
        var quote = new FuturesOptionTickDataV2ReadModel("option-0", date, 1, new(12, 0), 10, 9, 11, 1, 1, .2, 5000, .4, .01, 12, -3, .1)
        { ObservedAtUtc = clock.GetUtcNow(), ValidUntilUtc = clock.GetUtcNow().AddSeconds(5), SourceGeneration = Guid.NewGuid() };
        before.Apply(quote); after.Apply(quote);
        if (before.Expire().Length != 0 || after.Expire().Length != 0) throw new InvalidOperationException("Fresh quote expired.");
    }
    [Benchmark(Baseline = true), BenchmarkCategory("07-SparseExpiry")]
    public TradeRealtimeSnapshot[] ExpireBefore() => before.Expire();
    [Benchmark, BenchmarkCategory("07-SparseExpiry")]
    public TradeRealtimeSnapshot[] ExpireAfter() => after.Expire();
    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero); }
}
