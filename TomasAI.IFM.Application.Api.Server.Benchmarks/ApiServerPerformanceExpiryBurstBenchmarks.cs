using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>One synchronous deadline burst; identical registered trades and quotes are rebuilt outside timing.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig)), InvocationCount(1)]
public class ApiServerPerformanceExpiryBurstBenchmarks
{
    [Params(64, 4096)] public int Residents { get; set; }
    ApiServerPerformanceExpiryBaseline before = null!;
    TradeRealtimeCacheRegistry after = null!;

    [IterationSetup]
    public void Prepare()
    {
        var clock = new Clock();
        before = new(clock); after = new(clock);
        var date = new DateOnly(2026, 11, 7);
        for (var index = 0; index < Residents; index++)
        {
            var contract = "option-" + index;
            var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501 + index), StrategyKind = TradeStrategyKind.IronCondor,
                Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = contract, SignedQuantity = 1 }] };
            before.Register(trade, date); after.Register(trade, date);
            var quote = new FuturesOptionTickDataV2ReadModel(contract, date, 1, new(12, 0), 10, 9, 11, 1, 1, .2, 5000, .4, .01, 12, -3, .1)
            { ObservedAtUtc = clock.Now, ValidUntilUtc = clock.Now.AddSeconds(5), SourceGeneration = Guid.NewGuid() };
            before.Apply(quote); after.Apply(quote);
        }
        if (before.Expire().Length != 0 || after.Expire().Length != 0) throw new InvalidOperationException("Fresh quote expired.");
        clock.Now = clock.Now.AddSeconds(6);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("07-BurstExpiry")]
    public int BurstBefore()
    {
        var count = before.Expire().Length;
        if (count != Residents) throw new InvalidOperationException("Missing baseline expiry transitions.");
        return count;
    }

    [Benchmark, BenchmarkCategory("07-BurstExpiry")]
    public int BurstAfter()
    {
        var count = after.Expire().Length;
        if (count != Residents) throw new InvalidOperationException("Missing candidate expiry transitions.");
        return count;
    }

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
