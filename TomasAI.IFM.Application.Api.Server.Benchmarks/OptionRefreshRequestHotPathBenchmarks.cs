using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
public class OptionUnderlyingMappingBenchmarks
{
    readonly FuturesContractV3ReadModel[] _futures = Enumerable.Range(0, 24)
        .Select(index => Contract(index, new DateOnly(2027, 1, 1).AddMonths(index)))
        .ToArray();
    readonly DateOnly[] _expiries = Enumerable.Range(0, 10_000)
        .Select(index => new DateOnly(2027, 1, 1).AddDays(index % 730))
        .ToArray();

    [Benchmark(Baseline = true, Description = "Before: LINQ curve scan")]
    public int LinearCurveScan()
    {
        var checksum = 0;
        foreach (var expiry in _expiries)
        {
            var contractId = _futures.FirstOrDefault(contract => contract.LastTradeDate >= expiry)?.ContractId
                ?? _futures[^1].ContractId;
            checksum += contractId.Length;
        }
        return checksum;
    }

    [Benchmark(Description = "After: binary curve lookup")]
    public int BinaryCurveLookup()
    {
        var checksum = 0;
        foreach (var expiry in _expiries)
            checksum += OptionRefreshAlgorithms.FindUnderlyingContractId(_futures, expiry).Length;
        return checksum;
    }

    static FuturesContractV3ReadModel Contract(int index, DateOnly lastTradeDate) =>
        new($"ES-{index}", "ES", "ES", "ES", "FUT", "USD", "CME", "50", lastTradeDate, false);
}

[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
[InvocationCount(1)]
public class OptionRefreshFanOutBenchmarks
{
    const int RootCount = 31;
    static readonly TimeSpan ProviderLatency = TimeSpan.FromMilliseconds(5);
    static readonly int[] Roots = Enumerable.Range(0, RootCount).ToArray();

    [Benchmark(Baseline = true, Description = "Before: serial option-root loads")]
    public async Task LoadRootsSerially()
    {
        foreach (var _ in Roots)
            await LoadRootAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "After: two bounded option-root loads")]
    public Task LoadRootsWithBoundedConcurrency() =>
        Parallel.ForEachAsync(
            Roots,
            new ParallelOptions { MaxDegreeOfParallelism = 2 },
            static async (_, _) => await LoadRootAsync().ConfigureAwait(false));

    static Task LoadRootAsync() => Task.Delay(ProviderLatency);
}

[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
public class HttpContextAccessorHotPathBenchmarks
{
    readonly HttpContextAccessor _accessor = new();
    readonly DefaultHttpContext _context = new();

    [Benchmark(Baseline = true, Description = "Before: accessor set and clear")]
    public HttpContext TrackRequestWithAsyncLocal()
    {
        _accessor.HttpContext = _context;
        _accessor.HttpContext = null;
        return _context;
    }

    [Benchmark(Description = "After: no unused accessor")]
    public HttpContext DoNotTrackUnusedAccessor() => _context;
}
