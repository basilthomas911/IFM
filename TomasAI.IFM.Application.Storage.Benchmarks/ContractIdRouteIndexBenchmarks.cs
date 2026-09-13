using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ContractIdRouteIndexBenchmarks
{
    readonly ContractIdRouteIndex _unrouted = new(8);
    readonly ContractIdRouteIndex _oneRoute = new(8);
    readonly ContractIdRouteIndex _manyRoutes = new(8);
    long _sequence;

    [GlobalSetup]
    public void Setup()
    {
        _unrouted.RegisterKnownContract("ESZ6-unrouted");
        _oneRoute.Add(Route(1), "ESZ6-one");
        for (var index = 0; index < 64; index++) _manyRoutes.Add(Route(index + 1), "ESZ6-many");
    }

    [Benchmark(Baseline = true)]
    public MarketRouteLookupOutcome Unrouted()
    {
        var tick = new PositionMarketTick("ESZ6-unrouted", 5000m, Interlocked.Increment(ref _sequence), DateTime.UnixEpoch);
        return _unrouted.TryRoute(in tick, out _);
    }

    [Benchmark]
    public int OneRoute()
    {
        var tick = new PositionMarketTick("ESZ6-one", 5000m, Interlocked.Increment(ref _sequence), DateTime.UnixEpoch);
        _oneRoute.TryRoute(in tick, out var routes);
        return routes.Length;
    }

    [Benchmark]
    public int SixtyFourRoutes()
    {
        var tick = new PositionMarketTick("ESZ6-many", 5000m, Interlocked.Increment(ref _sequence), DateTime.UnixEpoch);
        _manyRoutes.TryRoute(in tick, out var routes);
        var checksum = 0;
        for (var index = 0; index < routes.Length; index++) checksum += routes[index].PortfolioId;
        return checksum;
    }

    static PortfolioFundTradeLeg Route(int value) => new(
        value, 2, 3, 4, Guid.NewGuid(), Guid.NewGuid(), TradeStrategyKind.IronCondor, 1);
}
