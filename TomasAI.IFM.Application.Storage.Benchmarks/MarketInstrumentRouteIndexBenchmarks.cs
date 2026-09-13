using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Model;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class MarketInstrumentRouteIndexBenchmarks
{
    readonly MarketInstrumentRouteIndex _unrouted = new(8);
    readonly MarketInstrumentRouteIndex _oneRoute = new(8);
    readonly MarketInstrumentRouteIndex _manyRoutes = new(8);
    long _sequence;

    [GlobalSetup]
    public void Setup()
    {
        _unrouted.RegisterKnownInstrument(1);
        _oneRoute.Add(Route(1), 2);
        for (var index = 0; index < 64; index++) _manyRoutes.Add(Route(index + 1), 3);
    }

    [Benchmark(Baseline = true)]
    public MarketRouteLookupOutcome Unrouted()
    {
        var tick = new PositionMarketTick(1, 5000m, Interlocked.Increment(ref _sequence), DateTime.UnixEpoch);
        return _unrouted.TryRoute(in tick, out _);
    }

    [Benchmark]
    public int OneRoute()
    {
        var tick = new PositionMarketTick(2, 5000m, Interlocked.Increment(ref _sequence), DateTime.UnixEpoch);
        _oneRoute.TryRoute(in tick, out var routes);
        return routes.Length;
    }

    [Benchmark]
    public int SixtyFourRoutes()
    {
        var tick = new PositionMarketTick(3, 5000m, Interlocked.Increment(ref _sequence), DateTime.UnixEpoch);
        _manyRoutes.TryRoute(in tick, out var routes);
        var checksum = 0;
        for (var index = 0; index < routes.Length; index++) checksum += routes[index].PortfolioId;
        return checksum;
    }

    static MarketPositionRoute Route(int value) => new(
        value, 2, 3, 4, Guid.NewGuid(), Guid.NewGuid(), TradeStrategyKind.IronCondor,
        "FuturesIronCondorTradePositionCommand", "thread", 1);
}

